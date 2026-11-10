using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Admin")]
    public class AdminController : Controller
    {
        private readonly IUnitOfWork _repo;
        private readonly ILogger<AdminController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly IRealtimeNotificationService _notifications;
        private readonly PayoutService _payoutService;
        private readonly IPlatformSettingsRepository _platformSettings;

        private static readonly BookingStatus[] ActiveStatuses =
        {
            BookingStatus.Pending,
            BookingStatus.AcceptedAwaitingPayment,
            BookingStatus.MarkAsPaid,
            BookingStatus.AwaitingClientStartConfirmation,
            BookingStatus.InProgress,
            BookingStatus.AwaitingClientConfirmation
        };

        public AdminController(IUnitOfWork repo, ILogger<AdminController> logger, UserManager<ApplicationUser> userManager, IConfiguration config, IRealtimeNotificationService notifications, PayoutService payoutService, IPlatformSettingsRepository platformSettings)
        {
            _repo = repo;
            _logger = logger;
            _userManager = userManager;
            _config = config;
            _notifications = notifications;
            _payoutService = payoutService;
            _platformSettings = platformSettings;
        }

        [HttpGet("")]
        [HttpGet("Dashboard")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewData["ActivePage"] = "Dashboard";

            var bookings = (await _repo.Bookings.GetAllBookings(ct))
                .Where(b => !b.IsDeleted)
                .ToList();
            var workers = (await _repo.Workers.GetAllWorkers(ct)).ToList();
            var clients = (await _repo.Clients.GetAllClients(ct)).ToList();
            var escrowsHeld = await _repo.Escrows.GetByStatus(EscrowStatus.Held, ct);

            var now = DateTime.Now;
            var completed = bookings.Where(b => b.BookingStatus == BookingStatus.Completed).ToList();
            var totalRevenue = completed.Sum(b => b.Amount);
            var revenueThisMonth = completed.Where(b => b.DateOfService.Month == now.Month && b.DateOfService.Year == now.Year).Sum(b => b.Amount);
            var commissionPercent = (await _platformSettings.GetAsync(ct)).CommissionPercent;
            var platformFeeShare = revenueThisMonth * (commissionPercent / 100m);

            var activeBookings = bookings.Count(b => ActiveStatuses.Contains(b.BookingStatus));
            var scheduledToday = bookings.Count(b => b.DateOfService.Date == now.Date);
            var activeWorkers = workers.Count(w => w.IsAvailable);
            var pendingWorkers = workers.Count(w => !w.IsAvailable);
            var newClientsThisMonth = clients.Count(c => c.DateCreated.Month == now.Month && c.DateCreated.Year == now.Year);

            var monthlyRevenue = new List<MonthlyRevenueDto>();
            var maxMonthly = 0m;
            for (var i = 5; i >= 0; i--)
            {
                var monthDate = now.AddMonths(-i);
                var amount = completed
                    .Where(b => b.DateOfService.Month == monthDate.Month && b.DateOfService.Year == monthDate.Year)
                    .Sum(b => b.Amount);
                monthlyRevenue.Add(new MonthlyRevenueDto
                {
                    Month = monthDate.ToString("MMM"),
                    Amount = amount,
                    Percentage = 0
                });
                if (amount > maxMonthly) maxMonthly = amount;
            }
            foreach (var m in monthlyRevenue)
                m.Percentage = maxMonthly > 0 ? (double)(m.Amount / maxMonthly) * 100d : 0d;

            var currentWeekStart = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
            var weeklyRevenue = new List<WeeklyRevenueDto>();
            var maxWeekly = 0m;
            for (var i = 5; i >= 0; i--)
            {
                var weekStart = currentWeekStart.AddDays(-7 * i);
                var weekEnd = weekStart.AddDays(7);
                var amount = completed
                    .Where(b => b.DateOfService.Date >= weekStart && b.DateOfService.Date < weekEnd)
                    .Sum(b => b.Amount);
                weeklyRevenue.Add(new WeeklyRevenueDto
                {
                    Week = weekStart.ToString("MMM d"),
                    Amount = amount,
                    Percentage = 0
                });
                if (amount > maxWeekly) maxWeekly = amount;
            }
            foreach (var week in weeklyRevenue)
                week.Percentage = maxWeekly > 0 ? (double)(week.Amount / maxWeekly) * 100d : 0d;
            var revenueThisWeek = completed
                .Where(b => b.DateOfService.Date >= currentWeekStart && b.DateOfService.Date < currentWeekStart.AddDays(7))
                .Sum(b => b.Amount);

            var recent = bookings
                .OrderByDescending(b => b.DateCreated)
                .Take(10)
                .Select(b => new AdminRecentBookingDto
                {
                    BookingId = FormatBookingId(b.Id, b.DateCreated),
                    ClientName = b.Client?.FullName?.ToString() ?? "N/A",
                    ClientInitials = GetInitials(b.Client?.FullName?.ToString()),
                    ClientLocation = b.Client?.State ?? "",
                    WorkerName = b.Worker?.FullName?.ToString() ?? "",
                    WorkerRating = b.Worker?.AverageRating,
                    ServiceType = b.ServiceType?.Name ?? "",
                    Amount = b.Amount,
                    Status = MapBookingStatus(b.BookingStatus),
                    DateOfService = b.DateOfService,
                    Address = b.Address?.ToString() ?? "",
                    TimeRange = b.TimeRange.ToString(),
                    TotalPaid = b.PaymentStatus == PaymentStatus.Successful ? b.Amount : null,
                    EscrowStatus = "Held in Escrow",
                    ClientEmail = b.Client?.Email?.ToString() ?? "",
                    WorkerPhone = b.Worker?.Contact?.ToString() ?? ""
                })
                .ToList();

            var payments = await _repo.Payments.GetAllPayments(ct);
            var ratings = await _repo.Ratings.GetAllRatingsAsync(ct);
            var audit = BuildAuditActivity(bookings, payments, ratings, now);

            var model = new AdminDashboardDto
            {
                TotalRevenue = totalRevenue,
                RevenueThisMonth = revenueThisMonth,
                RevenueThisWeek = revenueThisWeek,
                PlatformFeeShare = platformFeeShare,
                ActiveBookings = activeBookings,
                ScheduledToday = scheduledToday,
                ActiveWorkers = activeWorkers,
                PendingWorkers = pendingWorkers,
                TotalClients = clients.Count,
                NewClientsThisMonth = newClientsThisMonth,
                EscrowHeld = escrowsHeld.Sum(e => e.Amount),
                MonthlyRevenue = monthlyRevenue,
                WeeklyRevenue = weeklyRevenue,
                RecentBookings = recent,
                AuditActivity = audit
            };

            return View(model);
        }

        [HttpGet("Clients")]
        public async Task<IActionResult> Clients(CancellationToken ct)
        {
            ViewData["ActivePage"] = "Clients";

            var clients = (await _repo.Clients.GetAllClients(ct)).ToList();
            var bookings = (await _repo.Bookings.GetAllBookings(ct)).ToList();

            var clientBookings = bookings
                .Where(b => !b.IsDeleted)
                .GroupBy(b => b.ClientId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList());

            var items = clients.Select(c =>
            {
                var cBookings = clientBookings.TryGetValue(c.Id, out var list) ? list : new List<Booking>();
                var totalSpent = cBookings
                    .Where(b => b.PaymentStatus == PaymentStatus.Successful)
                    .Sum(b => b.Amount);
                var recent = cBookings
                    .OrderByDescending(b => b.DateCreated)
                    .Take(5)
                    .Select(b => new ClientRecentBookingDto
                    {
                        BookingId = FormatBookingId(b.Id, b.DateCreated),
                        Service = b.ServiceType?.Name ?? "",
                        Worker = b.Worker?.FullName?.ToString() ?? "",
                        Date = b.DateOfService,
                        Amount = b.Amount,
                        Status = MapBookingStatus(b.BookingStatus)
                    })
                    .ToList();
                var preferred = cBookings
                    .GroupBy(b => b.ServiceType?.Name ?? "N/A")
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault() ?? "";

                return new AdminClientDto
                {
                    Id = c.Id,
                    Name = c.FullName?.ToString() ?? "N/A",
                    Initials = GetInitials(c.FullName?.ToString()),
                    Email = c.Email?.ToString() ?? "",
                    Phone = c.PhoneNumber?.ToString() ?? "",
                    City = c.State ?? "",
                    TotalBookings = cBookings.Count,
                    Status = c.IsSuspended ? "Suspended" : "Active",
                    IsSuspended = c.IsSuspended,
                    TotalSpent = totalSpent,
                    DateJoined = c.DateCreated,
                    PreferredService = preferred,
                    RecentBookings = recent
                };
            }).ToList();

            return View(items);
        }

        [HttpPost("Clients/{clientId:guid}/Suspend")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleClientSuspension(Guid clientId, CancellationToken ct)
        {
            var client = await _repo.Clients.GetClientById(clientId, ct);
            if (client == null) return Json(new { ok = false, message = "Client not found." });

            var suspending = !client.IsSuspended;
            if (suspending) client.Suspend(User.Identity?.Name);
            else client.Reinstate(User.Identity?.Name);
            await _repo.Clients.UpdateClient(client, ct);

            if (suspending)
            {
                var bookings = (await _repo.Bookings.GetAllBookings(ct))
                    .Where(b => !b.IsDeleted && b.ClientId == client.Id && ActiveStatuses.Contains(b.BookingStatus)).ToList();
                foreach (var booking in bookings.Where(b => b.WorkerId != Guid.Empty))
                {
                    var workerNote = Notification.Create(booking.WorkerId, booking.Id, NotificationAudience.Worker,
                        "Client suspended", $"{client.FullName} has been suspended. Please review your active booking.",
                        "Suspended", "warning", "fa-user-slash", "View Booking", $"/Worker-Booking-Details/{booking.Id}", true, User.Identity?.Name);
                    await _repo.Notifications.CreateAsync(workerNote, ct);
                }
                var clientNote = Notification.Create(client.Id, Guid.Empty, NotificationAudience.Client,
                    "Your account has been suspended", "Your account is suspended. Booking and service access is restricted. Contact support for assistance.",
                    "Suspended", "danger", "fa-user-slash", "Contact Support", "/Home/Contact", true, User.Identity?.Name);
                await _repo.Notifications.CreateAsync(clientNote, ct);
            }
            await _repo.SaveChangesAsync(ct);
            return Json(new { ok = true, suspended = suspending, message = suspending ? "Client suspended." : "Client reinstated." });
        }

        [HttpGet("Workers")]
        public async Task<IActionResult> Workers(CancellationToken ct)
        {
            ViewData["ActivePage"] = "Workers";

            var workers = await _repo.Workers.GetAllWorkersWithBookingsAsync(ct);
            var items = new List<AdminWorkerDto>();

            foreach (var w in workers.Where(w => !w.IsDeleted))
            {
                var bankDetail = await _repo.WorkerBankDetails.GetActiveByWorkerIdAsync(w.Id, ct);
                var completedJobs = w.Bookings.Count(b => b.BookingStatus == BookingStatus.Completed);
                items.Add(new AdminWorkerDto
                {
                    Id = w.Id,
                    Name = w.FullName?.ToString() ?? "N/A",
                    Initials = GetInitials(w.FullName?.ToString()),
                    AverageRating = w.AverageRating > 0 ? w.AverageRating : null,
                    RatingCount = w.TotalRating,
                    Phone = w.Contact?.ToString() ?? "",
                    Email = w.Email?.ToString() ?? "",
                    Specialty = w.ServiceType?.Name ?? "",
                    CompletedJobs = completedJobs,
                    KycStatus = w.IsSuspended ? "Suspended" : "Verified",
                    IsSuspended = w.IsSuspended,
                    Availability = w.IsAvailable ? "Available" : "Off-Duty",
                    HasBankDetails = bankDetail != null,
                    PayoutAccount = bankDetail != null
                        ? MaskAccount(bankDetail.AccountNumber, bankDetail.BankName)
                        : "No bank details",
                    DateJoined = w.DateCreated,
                    City = w.State ?? ""
                });
            }

            return View(items);
        }

        [HttpPost("Workers/{workerId:guid}/Suspend")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleWorkerSuspension(Guid workerId, CancellationToken ct)
        {
            var worker = await _repo.Workers.GetWorkerById(workerId, ct);
            if (worker == null) return Json(new { ok = false, message = "Worker not found." });

            var suspending = !worker.IsSuspended;
            if (suspending) worker.Suspend(User.Identity?.Name);
            else worker.Reinstate(User.Identity?.Name);
            await _repo.Workers.UpdateWorker(worker, ct);

            if (suspending)
            {
                var bookings = (await _repo.Bookings.GetAllBookings(ct))
                    .Where(b => !b.IsDeleted && b.WorkerId == worker.Id && ActiveStatuses.Contains(b.BookingStatus)).ToList();
                foreach (var booking in bookings)
                {
                    var clientNote = Notification.Create(booking.ClientId, booking.Id, NotificationAudience.Client,
                        "Worker suspended", $"{worker.FullName} has been suspended. Please contact support about your active booking.",
                        "Suspended", "warning", "fa-user-slash", "View Booking", $"/Client-Booking-Details/{booking.Id}", true, User.Identity?.Name);
                    await _repo.Notifications.CreateAsync(clientNote, ct);
                }
                var workerNote = Notification.Create(worker.Id, Guid.Empty, NotificationAudience.Worker,
                    "Your account has been suspended", "Your account is suspended. Worker services and booking actions are restricted. Contact support for assistance.",
                    "Suspended", "danger", "fa-user-slash", "Contact Support", "/Home/Contact", true, User.Identity?.Name);
                await _repo.Notifications.CreateAsync(workerNote, ct);
            }
            await _repo.SaveChangesAsync(ct);
            return Json(new { ok = true, suspended = suspending, message = suspending ? "Worker suspended." : "Worker reinstated." });
        }

        [HttpGet("Payouts")]
        public async Task<IActionResult> Payouts(CancellationToken ct)
        {
            ViewData["ActivePage"] = "Payouts";

            var escrows = (await _repo.Escrows.GetAllEscrowsAsync(ct))
                .Where(e => e.Status == EscrowStatus.Held
                    || e.TransferStatus == PayoutTransferStatus.LegacyUnverified)
                .ToList();

            var items = new List<AdminPayoutDto>();
            foreach (var escrow in escrows)
            {
                var booking = await _repo.Bookings.GetBookingById(escrow.BookingId, ct);
                if (booking == null || booking.IsDeleted || booking.BookingStatus != BookingStatus.Completed || booking.PaymentStatus != PaymentStatus.Successful)
                    continue;

                var worker = await _repo.Workers.GetWorkerById(booking.WorkerId, ct);
                var bankDetail = await _repo.WorkerBankDetails.GetActiveByWorkerIdAsync(booking.WorkerId, ct);
                var clientName = booking.Client?.FullName?.ToString() ?? "Unknown";

                items.Add(new AdminPayoutDto
                {
                    BookingId = booking.Id,
                    BookingRef = FormatBookingId(booking.Id, booking.DateCreated),
                    WorkerName = worker?.FullName?.ToString() ?? "Unknown",
                    WorkerInitials = GetInitials(worker?.FullName?.ToString()),
                    ClientName = clientName,
                    Gross = booking.OriginalAmount,
                    Commission = escrow.CommissionAmount,
                    Net = escrow.WorkerShare,
                    HasBankDetails = bankDetail != null,
                    PayoutAccount = bankDetail != null ? MaskAccount(bankDetail.AccountNumber, bankDetail.BankName) : "No bank details",
                    PayoutStatus = escrow.TransferStatus.ToString()
                });
            }

            return View(items);
        }

        [HttpPost("Payouts/Pay")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayOutBooking(Guid bookingId, CancellationToken ct)
        {
            var admin = User.Identity?.Name;
            try
            {
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
                if (booking == null || booking.IsDeleted)
                    return Json(new { ok = false, message = "Booking not found." });

                var escrow = await _repo.Escrows.GetByBookingId(bookingId, ct);
                if (escrow == null || escrow.Status != EscrowStatus.Held)
                    return Json(new { ok = false, message = "This payout is not pending. It may have already been paid." });

                var result = await _payoutService.PayoutAsync(booking, admin, ct);
                return Json(new { ok = result.Success, message = result.Message, reference = result.ProviderReference, status = result.TransferStatus });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin payout for booking {BookingId} failed.", bookingId);
                return Json(new { ok = false, message = $"Payout failed: {ex.Message}" });
            }
        }

        [HttpGet("ServiceTypes")]
        public async Task<IActionResult> ServiceTypes(CancellationToken ct)
        {
            ViewData["ActivePage"] = "ServiceTypes";

            var services = await _repo.ServiceTypes.GetAllIncludingDeletedAsync(ct);
            var bookings = (await _repo.Bookings.GetAllBookings(ct))
                .Where(b => !b.IsDeleted)
                .ToList();
            var workers = (await _repo.Workers.GetAllWorkers(ct)).ToList();

            var items = services.Select(s =>
            {
                var sBookings = bookings.Where(b => b.ServiceTypeId == s.Id).ToList();
                return new AdminServiceTypeDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    Duration = "2 - 3 hrs",
                    Status = s.IsDeleted ? "Deactivated" : "Active",
                    TotalBookings = sBookings.Count,
                    WorkerCount = workers.Count(w => w.ServiceTypeId == s.Id),
                    Revenue = sBookings.Where(b => b.BookingStatus == BookingStatus.Completed).Sum(b => b.Amount)
                };
            }).ToList();

            return View(items);
        }

        [HttpGet("Bookings")]
        public async Task<IActionResult> Bookings(CancellationToken ct)
        {
            ViewData["ActivePage"] = "Bookings";

            var bookings = (await _repo.Bookings.GetAllBookings(ct))
                .Where(b => !b.IsDeleted)
                .ToList();
            var escrows = (await _repo.Escrows.GetAllEscrowsAsync(ct))
                .ToDictionary(e => e.BookingId, e => e);

            ViewBag.Workers = (await _repo.Workers.GetAllWorkers(ct))
                .Where(w => !w.IsDeleted)
                .OrderBy(w => w.FullName?.ToString() ?? "")
                .ToList();

            var commissionPercent = _config.GetValue<decimal>("Commission:Rate");
            if (commissionPercent <= 0) commissionPercent = 15;

            var items = bookings.Select(b =>
            {
                var hasEscrow = escrows.TryGetValue(b.Id, out var escrow);
                var gross = b.Amount;
                var commission = hasEscrow && escrow != null ? escrow.CommissionAmount : gross * (commissionPercent / 100m);
                return new AdminBookingDto
                {
                    Id = b.Id,
                    BookingId = FormatBookingId(b.Id, b.DateCreated),
                    ClientName = b.Client?.FullName?.ToString() ?? "N/A",
                    ClientInitials = GetInitials(b.Client?.FullName?.ToString()),
                    ClientLocation = b.Client?.State ?? "",
                    ClientPhone = b.Client?.PhoneNumber?.ToString() ?? "",
                    ClientEmail = b.Client?.Email?.ToString() ?? "",
                    WorkerName = b.Worker?.FullName?.ToString() ?? "",
                    WorkerInitials = GetInitials(b.Worker?.FullName?.ToString()),
                    WorkerRating = b.Worker?.AverageRating,
                    WorkerPhone = b.Worker?.Contact?.ToString() ?? "",
                    ServiceType = b.ServiceType?.Name ?? "",
                    DateOfService = b.DateOfService,
                    TimeRange = b.TimeRange.ToString(),
                    Status = b.BookingStatus,
                    Amount = b.Amount,
                    EscrowStatus = hasEscrow && escrow != null ? MapEscrowStatus(escrow.Status) : "N/A",
                    Commission = commission,
                    WorkerNetPayout = gross - commission,
                    Address = b.Address?.ToString() ?? "",
                    SpecialInstructions = "",
                    IsAssigned = b.Worker != null
                };
            })
            .OrderByDescending(b => b.DateOfService)
            .ToList();

            return View(items);
        }

        [HttpGet("Payments")]
        public async Task<IActionResult> Payments(CancellationToken ct)
        {
            ViewData["ActivePage"] = "Payments";

            var payments = await _repo.Payments.GetAllPayments(ct);
            var bookings = (await _repo.Bookings.GetAllBookings(ct))
                .Where(b => !b.IsDeleted)
                .ToDictionary(b => b.Id, b => b);
            var escrows = (await _repo.Escrows.GetAllEscrowsAsync(ct)).ToList();

            var escrowByPayment = escrows.ToDictionary(e => e.PaymentId, e => e);

            IEnumerable<AdminPaymentDto> items = payments
                .Where(p => !p.IsDeleted)
                .Select(p =>
                {
                    bookings.TryGetValue(p.BookingId, out var booking);
                    var escrow = escrowByPayment.TryGetValue(p.Id, out var e) ? e : null;
                    var bookedAmount = booking?.Amount ?? 0m;
                    var commission = escrow?.CommissionAmount ?? (bookedAmount * 0.15m);
                    return new AdminPaymentDto
                    {
                        Id = p.Id,
                        TransactionRef = FormatTransactionRef(p.PaymentReference),
                        BookingRef = booking != null ? FormatBookingId(booking.Id, booking.DateCreated) : p.BookingId.ToString().Substring(0, 6),
                        PayerName = booking?.Client?.FullName?.ToString() ?? "N/A",
                        PayeeName = booking?.Worker?.FullName?.ToString() ?? "Pending Assign",
                        GrossAmount = p.Amount,
                        Fee = p.Status == PaymentStatus.Refunded ? 0 : commission,
                        EscrowStatus = escrow != null ? MapEscrowStatus(escrow.Status) : (p.Status == PaymentStatus.Refunded ? "Refunded" : "In Escrow"),
                        Gateway = MapGateway(p.PaymentMethod),
                        GatewayRef = p.TransactionId ?? p.PaymentReference,
                        Timestamp = p.DateCreated,
                        WorkerNetSettlement = p.Amount - commission,
                        PayoutDestination = "N/A",
                        Status = p.Status
                    };
                })
                .OrderByDescending(p => p.Timestamp);

            return View(items.ToList());
        }

        [HttpGet("Reviews")]
        public async Task<IActionResult> Reviews(CancellationToken ct)
        {
            ViewData["ActivePage"] = "Reviews";

            var ratings = await _repo.Ratings.GetAllRatingsAsync(ct);
            var clients = (await _repo.Clients.GetAllClients(ct))
                .ToDictionary(c => c.Id, c => c);

            var items = ratings
                .Where(r => !r.IsDeleted)
                .Select(r =>
                {
                    clients.TryGetValue(r.ClientId, out var client);
                    return new AdminReviewDto
                    {
                        Id = r.Id,
                        ClientName = client?.FullName?.ToString() ?? "N/A",
                        ClientInitials = GetInitials(client?.FullName?.ToString()),
                        WorkerName = r.Worker?.FullName?.ToString() ?? "N/A",
                        WorkerInitials = GetInitials(r.Worker?.FullName?.ToString()),
                        WorkerId = FormatWorkerId(r.WorkerId, r.Worker?.DateCreated),
                        RatingValue = r.RatingValue,
                        ServiceType = r.Worker?.ServiceType?.Name ?? "",
                        Comment = r.Comment ?? "",
                        ModerationStatus = "Approved",
                        Date = r.DateCreated
                    };
                })
                .OrderByDescending(r => r.Date)
                .ToList();

            return View(items);
        }

        [HttpGet("Settings")]
        public async Task<IActionResult> Settings(CancellationToken ct)
        {
            ViewData["ActivePage"] = "Settings";
            var settings = await _platformSettings.GetAsync(ct);

            var adminUsers = new List<AdminUserDto>();
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var user in admins)
            {
                var roles = await _userManager.GetRolesAsync(user);
                adminUsers.Add(new AdminUserDto
                {
                    Name = user.UserName ?? user.Email ?? "Admin",
                    Initials = GetInitials(user.UserName ?? user.Email),
                    Email = user.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "Admin",
                    Status = "Active",
                    IsOwner = user.Email == _config["Email:Smtp:From"]
                });
            }

            var model = new AdminSettingsDto
            {
                PlatformName = settings.PlatformName,
                SupportEmail = settings.SupportEmail,
                CustomerServicePhone = settings.CustomerServicePhone,
                BaseCurrency = settings.BaseCurrency,
                Timezone = settings.Timezone,
                MaintenanceMode = settings.MaintenanceMode,
                AutoMatchCleanerTimeoutMinutes = settings.AutoMatchCleanerTimeoutMinutes,
                MinimumAdvanceNoticeHours = settings.MinimumAdvanceNoticeHours,
                FreeCancellationWindowHours = settings.FreeCancellationWindowHours,
                MaxActiveJobsPerCleanerPerDay = settings.MaxActiveJobsPerCleanerPerDay,
                CommissionPercent = settings.CommissionPercent,
                AutoReleaseEscrowDelayHours = settings.AutoReleaseEscrowDelayHours,
                MinimumPayoutAmount = settings.MinimumPayoutAmount,
                EmailAlertsOnNewBooking = settings.EmailAlertsOnNewBooking,
                SmsToCleanerOnAssignment = settings.SmsToCleanerOnAssignment,
                EmailBookingConfirmation = settings.EmailBookingConfirmation,
                EmailBookingCancellation = settings.EmailBookingCancellation,
                EmailPaymentNotifications = settings.EmailPaymentNotifications,
                SmsBookingNotifications = settings.SmsBookingNotifications,
                AdminUsers = adminUsers
            };

            return View(model);
        }

        [HttpPost("Settings")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(AdminSettingsDto input, CancellationToken ct)
        {
            ViewData["ActivePage"] = "Settings";
            input.Timezone = NormalizeTimeZoneId(input.Timezone);
            if (string.IsNullOrWhiteSpace(input.PlatformName) || input.PlatformName.Trim().Length > 100)
                ModelState.AddModelError(nameof(input.PlatformName), "Enter a platform name of 1 to 100 characters.");
            if (!System.Net.Mail.MailAddress.TryCreate(input.SupportEmail, out _))
                ModelState.AddModelError(nameof(input.SupportEmail), "Enter a valid support email address.");
            if (string.IsNullOrWhiteSpace(input.CustomerServicePhone) || input.CustomerServicePhone.Length > 32)
                ModelState.AddModelError(nameof(input.CustomerServicePhone), "Enter a customer service phone number of up to 32 characters.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(input.BaseCurrency ?? "", "^[A-Z]{3}$"))
                ModelState.AddModelError(nameof(input.BaseCurrency), "Use a three-letter uppercase currency code, such as NGN.");
            if (!IsValidTimeZoneId(input.Timezone))
                ModelState.AddModelError(nameof(input.Timezone), "Enter a valid IANA or Windows timezone ID, such as Africa/Lagos.");
            if (input.CommissionPercent is < 0 or > 50)
                ModelState.AddModelError(nameof(input.CommissionPercent), "Commission must be between 0 and 50 percent.");
            if (input.AutoMatchCleanerTimeoutMinutes is < 1 or > 1440)
                ModelState.AddModelError(nameof(input.AutoMatchCleanerTimeoutMinutes), "Timeout must be between 1 and 1440 minutes.");
            if (input.MinimumAdvanceNoticeHours is < 0 or > 8760)
                ModelState.AddModelError(nameof(input.MinimumAdvanceNoticeHours), "Advance notice must be between 0 and 8760 hours.");
            if (input.FreeCancellationWindowHours is < 0 or > 8760)
                ModelState.AddModelError(nameof(input.FreeCancellationWindowHours), "Cancellation window must be between 0 and 8760 hours.");
            if (input.MaxActiveJobsPerCleanerPerDay is < 1 or > 100)
                ModelState.AddModelError(nameof(input.MaxActiveJobsPerCleanerPerDay), "Active jobs must be between 1 and 100.");
            if (input.AutoReleaseEscrowDelayHours is < 0 or > 8760)
                ModelState.AddModelError(nameof(input.AutoReleaseEscrowDelayHours), "Escrow delay must be between 0 and 8760 hours.");
            if (input.MinimumPayoutAmount < 0)
                ModelState.AddModelError(nameof(input.MinimumPayoutAmount), "Minimum payout cannot be negative.");

            if (!ModelState.IsValid)
            {
                input.AdminUsers = (await _userManager.GetUsersInRoleAsync("Admin")).Select(u => new AdminUserDto
                { Name = u.UserName ?? u.Email ?? "Admin", Email = u.Email ?? "", Role = "Admin", Status = "Active", Initials = GetInitials(u.UserName ?? u.Email) }).ToList();
                return View("Settings", input);
            }
            var value = new PlatformSettings
            {
                PlatformName = input.PlatformName.Trim(), SupportEmail = input.SupportEmail.Trim(),
                CustomerServicePhone = input.CustomerServicePhone.Trim(), BaseCurrency = input.BaseCurrency,
                Timezone = input.Timezone, MaintenanceMode = input.MaintenanceMode,
                AutoMatchCleanerTimeoutMinutes = input.AutoMatchCleanerTimeoutMinutes,
                MinimumAdvanceNoticeHours = input.MinimumAdvanceNoticeHours,
                FreeCancellationWindowHours = input.FreeCancellationWindowHours,
                MaxActiveJobsPerCleanerPerDay = input.MaxActiveJobsPerCleanerPerDay,
                CommissionPercent = input.CommissionPercent, AutoReleaseEscrowDelayHours = input.AutoReleaseEscrowDelayHours,
                MinimumPayoutAmount = input.MinimumPayoutAmount, EmailAlertsOnNewBooking = input.EmailAlertsOnNewBooking,
                SmsToCleanerOnAssignment = input.SmsToCleanerOnAssignment,
                EmailBookingConfirmation = input.EmailBookingConfirmation,
                EmailBookingCancellation = input.EmailBookingCancellation,
                EmailPaymentNotifications = input.EmailPaymentNotifications, SmsBookingNotifications = input.SmsBookingNotifications
            };
            await _platformSettings.SaveAsync(value, ct);
            TempData["SettingsSaved"] = "Platform settings saved.";
            return RedirectToAction(nameof(Settings));
        }

        private static string NormalizeTimeZoneId(string? timeZoneId) =>
            string.Equals(timeZoneId?.Trim(), "WAT", StringComparison.OrdinalIgnoreCase) ? "Africa/Lagos" : timeZoneId?.Trim() ?? string.Empty;

        private static bool IsValidTimeZoneId(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId)) return false;
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId.Trim(), out var windowsId) &&
                       TimeZoneInfo.GetSystemTimeZones().Any(zone => zone.Id == windowsId);
            }
            catch (InvalidTimeZoneException)
            {
                return false;
            }
        }

        private static bool IsValidServiceTypeInput(AdminServiceTypeInput input) =>
            !string.IsNullOrWhiteSpace(input.Name) && input.Name.Trim().Length is >= 10 and <= 50 &&
            !string.IsNullOrWhiteSpace(input.Description) && input.Description.Trim().Length is >= 10 and <= 200;

        [HttpPost("ServiceTypes/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateServiceType(AdminServiceTypeInput input, CancellationToken ct)
        {
            if (!IsValidServiceTypeInput(input))
                return Json(new { ok = false, message = "Name (10-50 chars) and description (10-200 chars) are required." });

            try
            {
                if (await _repo.ServiceTypes.CheckExistingByName(input.Name.Trim(), ct))
                    return Json(new { ok = false, message = $"A service named '{input.Name.Trim()}' already exists." });

                var serviceType = ServiceType.Create(input.Name.Trim(), input.Description.Trim(), User.Identity?.Name);
                await _repo.ServiceTypes.AddAsync(serviceType, ct);
                await _repo.SaveChangesAsync(ct);

                _logger.LogInformation("Admin created service type '{Name}'.", input.Name.Trim());
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create service type '{Name}'.", input.Name);
                return Json(new { ok = false, message = "Failed to create the service type. Please try again." });
            }
        }

        [HttpPost("ServiceTypes/Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditServiceType(AdminServiceTypeInput input, CancellationToken ct)
        {
            if (input.Id == null || input.Id == Guid.Empty)
                return Json(new { ok = false, message = "Missing service id." });
            if (!IsValidServiceTypeInput(input))
                return Json(new { ok = false, message = "Name (10-50 chars) and description (10-200 chars) are required." });

            try
            {
                var serviceType = await _repo.ServiceTypes.GetByIdAsync(input.Id.Value, ct);
                if (serviceType == null)
                    return Json(new { ok = false, message = "Service type not found." });

                serviceType.UpdateService(input.Name.Trim(), input.Description.Trim(), User.Identity?.Name);
                await _repo.ServiceTypes.UpdateAsync(serviceType, ct);
                await _repo.SaveChangesAsync(ct);

                _logger.LogInformation("Admin updated service type '{Name}'.", input.Name.Trim());
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update service type id '{Id}'.", input.Id);
                return Json(new { ok = false, message = "Failed to update the service type. Please try again." });
            }
        }

        [HttpPost("ServiceTypes/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteServiceType(Guid id, CancellationToken ct)
        {
            try
            {
                var serviceType = await _repo.ServiceTypes.GetByIdAsync(id, ct);
                if (serviceType == null)
                    return Json(new { ok = false, message = "Service type not found." });

                serviceType.MarkAsDeleted(User.Identity?.Name);
                await _repo.ServiceTypes.UpdateAsync(serviceType, ct);
                await _repo.SaveChangesAsync(ct);

                _logger.LogInformation("Admin deactivated service type '{Name}' (id {Id}).", serviceType.Name, id);
                await NotifyServiceTypeStatusChangeAsync(serviceType, deactivated: true, ct);
                return Json(new { ok = true, message = $"{serviceType.Name} deactivated. Affected clients and cleaners have been notified." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate service type id '{Id}'.", id);
                return Json(new { ok = false, message = "Failed to deactivate the service type. Please try again." });
            }
        }

        [HttpPost("ServiceTypes/{id:guid}/Restore")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreServiceType(Guid id, CancellationToken ct)
        {
            try
            {
                var serviceType = await _repo.ServiceTypes.GetByIdAsync(id, ct);
                if (serviceType == null)
                    return Json(new { ok = false, message = "Service type not found." });

                serviceType.Restore(User.Identity?.Name);
                await _repo.ServiceTypes.UpdateAsync(serviceType, ct);
                await _repo.SaveChangesAsync(ct);

                _logger.LogInformation("Admin reactivated service type '{Name}' (id {Id}).", serviceType.Name, id);
                await NotifyServiceTypeStatusChangeAsync(serviceType, deactivated: false, ct);
                return Json(new { ok = true, message = $"{serviceType.Name} is live again. Cleaners offering it have been notified." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reactivate service type id '{Id}'.", id);
                return Json(new { ok = false, message = "Failed to reactivate the service type. Please try again." });
            }
        }

        [HttpGet("Notifications/Feed")]
        public async Task<IActionResult> NotificationFeed(CancellationToken ct)
        {
            var items = await BuildNotificationFeedAsync(ct);
            return Json(items);
        }

        private async Task<List<AdminNotificationDto>> BuildNotificationFeedAsync(CancellationToken ct)
        {
            var now = DateTime.Now;
            var items = new List<AdminNotificationDto>();

            var bookings = (await _repo.Bookings.GetAllBookings(ct))
                .Where(b => !b.IsDeleted)
                .ToList();

            foreach (var b in bookings.OrderByDescending(x => x.DateCreated).Take(6))
            {
                var status = MapBookingStatus(b.BookingStatus);
                var icon = b.BookingStatus switch
                {
                    BookingStatus.Completed => "fa-circle-check",
                    BookingStatus.Pending => "fa-calendar-plus",
                    BookingStatus.Cancelled or BookingStatus.Rejected or BookingStatus.Expired => "fa-circle-xmark",
                    _ => "fa-spinner"
                };
                var variant = b.BookingStatus switch
                {
                    BookingStatus.Completed => "emerald",
                    BookingStatus.Pending => "sky",
                    BookingStatus.Cancelled or BookingStatus.Rejected or BookingStatus.Expired => "rose",
                    _ => "amber"
                };

                var bookingRef = FormatBookingId(b.Id, b.DateCreated);
                items.Add(new AdminNotificationDto
                {
                    Title = $"{status} — {bookingRef}",
                    Message = $"{b.Client?.FullName?.ToString() ?? "Client"} • {b.ServiceType?.Name ?? "Cleaning"} • ₦{b.Amount:N0}",
                    Href = "/Admin/Bookings",
                    Icon = icon,
                    Variant = variant,
                    When = b.DateCreated,
                    TimeAgo = FormatTimeAgo(b.DateCreated, now)
                });
            }

            var deactivatedServices = (await _repo.ServiceTypes.GetAllIncludingDeletedAsync(ct))
                .Where(s => s.IsDeleted)
                .OrderByDescending(s => s.DateModified)
                .Take(3);
            foreach (var s in deactivatedServices)
            {
                var evt = s.DateModified ?? s.DateCreated;
                items.Add(new AdminNotificationDto
                {
                    Title = $"Service deactivated — {s.Name}",
                    Message = "No longer bookable. Cleaners offering it were notified.",
                    Href = "/Admin/ServiceTypes",
                    Icon = "fa-triangle-exclamation",
                    Variant = "rose",
                    When = evt,
                    TimeAgo = FormatTimeAgo(evt, now)
                });
            }

            var newClients = (await _repo.Clients.GetAllClients(ct))
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.DateCreated)
                .Take(3);
            foreach (var c in newClients)
            {
                items.Add(new AdminNotificationDto
                {
                    Title = "New client registered",
                    Message = $"{c.FullName?.ToString() ?? "Client"} • {c.State}",
                    Href = "/Admin/Clients",
                    Icon = "fa-user-plus",
                    Variant = "sky",
                    When = c.DateCreated,
                    TimeAgo = FormatTimeAgo(c.DateCreated, now)
                });
            }

            var newWorkers = (await _repo.Workers.GetAllWorkers(ct))
                .Where(w => !w.IsDeleted)
                .OrderByDescending(w => w.DateCreated)
                .Take(3);
            foreach (var w in newWorkers)
            {
                items.Add(new AdminNotificationDto
                {
                    Title = "New cleaner joined",
                    Message = $"{w.FullName?.ToString() ?? "Cleaner"} • {w.State}",
                    Href = "/Admin/Workers",
                    Icon = "fa-user-check",
                    Variant = "amber",
                    When = w.DateCreated,
                    TimeAgo = FormatTimeAgo(w.DateCreated, now)
                });
            }

            return items
                .OrderByDescending(i => i.When)
                .Take(8)
                .ToList();
        }

        private async Task NotifyServiceTypeStatusChangeAsync(ServiceType serviceType, bool deactivated, CancellationToken ct)
        {
            try
            {
                var allClients = (await _repo.Clients.GetAllClients(ct))
                    .Where(c => !c.IsDeleted)
                    .Distinct()
                    .ToList();

                var allWorkers = (await _repo.Workers.GetAllWorkers(ct))
                    .Where(w => !w.IsDeleted)
                    .Distinct()
                    .ToList();

                var title = deactivated ? "Service deactivated" : "Service available again";
                var message = deactivated
                    ? $"{serviceType.Name} has been deactivated and is no longer bookable. If you have upcoming bookings for this service, please contact admin."
                    : $"{serviceType.Name} is back online and available for booking again.";
                var status = deactivated ? "Deactivated" : "Active";
                var tone = deactivated ? "warning" : "success";
                var icon = deactivated ? "fa-triangle-exclamation" : "fa-circle-check";

                foreach (var worker in allWorkers)
                {
                    var notification = Notification.Create(
                        worker.Id, Guid.Empty, NotificationAudience.Worker, title, message, status, tone, icon,
                        "Go to Dashboard", "/Worker-Dashboard", needsAttention: deactivated, createdBy: User.Identity?.Name);
                    await _repo.Notifications.CreateAsync(notification, ct);

                    await _notifications.NotifyUserAsync(
                        worker.Email?.ToString(), title, message, tone, "/Worker-Dashboard");
                }

                foreach (var client in allClients)
                {
                    var notification = Notification.Create(
                        client.Id, Guid.Empty, NotificationAudience.Client, title, message, status, tone, icon,
                        "Go to Dashboard", "/Client-Dashboard", needsAttention: deactivated, createdBy: User.Identity?.Name);
                    await _repo.Notifications.CreateAsync(notification, ct);

                    await _notifications.NotifyUserAsync(
                        client.Email?.ToString(), title, message, tone, "/Client-Dashboard");
                }

                await _notifications.NotifyAdminAsync(
                    title,
                    $"Admin {User.Identity?.Name} {(deactivated ? "deactivated" : "reactivated")} {serviceType.Name}.",
                    tone,
                    "/Admin/ServiceTypes");

                await _repo.SaveChangesAsync(ct);
                _logger.LogInformation("Notified {WorkerCount} workers and {ClientCount} clients of service type '{Name}' status change (deactivated={Deactivated}).",
                    allWorkers.Count, allClients.Count, serviceType.Name, deactivated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify users of service type '{Name}' status change (deactivated={Deactivated}).", serviceType.Name, deactivated);
            }
        }

        [HttpPost("Bookings/{bookingId:guid}/Assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignBookingWorker(Guid bookingId, Guid workerId, CancellationToken ct)
        {
            try
            {
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
                if (booking == null || booking.IsDeleted)
                    return Json(new { ok = false, message = "Booking not found." });

                var worker = await _repo.Workers.GetWorkerById(workerId, ct);
                if (worker == null || worker.IsDeleted)
                    return Json(new { ok = false, message = "Selected cleaner does not exist." });

                booking.AssignWorker(workerId, User.Identity?.Name);
                await _repo.Bookings.UpdateBooking(booking, ct);
                await _repo.SaveChangesAsync(ct);

                _logger.LogInformation("Admin assigned worker '{WorkerId}' to booking '{BookingId}'.", workerId, bookingId);
                return Json(new { ok = true, message = $"Cleaner assigned to booking {FormatBookingId(booking.Id, booking.DateCreated)}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign worker '{WorkerId}' to booking '{BookingId}'.", workerId, bookingId);
                return Json(new { ok = false, message = ex.Message });
            }
        }

        [HttpPost("Bookings/{bookingId:guid}/Status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBookingStatus(Guid bookingId, string status, string? reason, CancellationToken ct)
        {
            var admin = User.Identity?.Name;
            try
            {
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
                if (booking == null || booking.IsDeleted)
                    return Json(new { ok = false, message = "Booking not found." });

                switch (status)
                {
                    case "InProgress":
                        if (booking.BookingStatus == BookingStatus.MarkAsPaid)
                        {
                            booking.RequestStartJob();
                            booking.StartJob();
                        }
                        else if (booking.BookingStatus == BookingStatus.AwaitingClientStartConfirmation)
                        {
                            booking.StartJob();
                        }
                        else
                        {
                            return Json(new { ok = false, message = "Booking can only be moved to In Progress once it is paid and start-confirmed." });
                        }
                        break;

                    case "Completed":
                        booking.MarkAsCompleted();

                        var escrow = await _repo.Escrows.GetByBookingId(bookingId, ct);
                        if (escrow != null && escrow.Status == EscrowStatus.Held)
                        {
                            var payout = await _payoutService.PayoutAsync(booking, admin, ct);
                            if (!payout.Success && payout.ProviderReference == null)
                            {
                                _logger.LogInformation("Admin completed booking {BookingId}; payout deferred: {Message}", bookingId, payout.Message);
                            }
                        }
                        break;

                    case "Expired":
                        booking.MarkAsExpired();
                        break;

                    case "Refund":
                        booking.RefundPayment();
                        var refundEscrow = await _repo.Escrows.GetByBookingId(bookingId, ct);
                        if (refundEscrow != null && refundEscrow.Status == EscrowStatus.Held)
                        {
                            refundEscrow.MarkAsRefunded(admin);
                            await _repo.Escrows.UpdateEscrow(refundEscrow, ct);
                        }
                        break;

                    default:
                        return Json(new { ok = false, message = "Unknown status action." });
                }

                await _repo.Bookings.UpdateBooking(booking, ct);
                await _repo.SaveChangesAsync(ct);

                _logger.LogInformation("Admin set booking '{BookingId}' to {Status}. Reason: {Reason}", bookingId, status, reason ?? "None");
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update booking '{BookingId}' status to {Status}.", bookingId, status);
                return Json(new { ok = false, message = ex.Message });
            }
        }

        private static string FormatBookingId(Guid id, DateTime dateCreated)
        {
            var seed = id != Guid.Empty ? id.ToString("N").Substring(0, 8).ToUpper() : dateCreated.Ticks.ToString().Substring(0, 8);
            return $"#BK-{seed}";
        }

        private static string FormatTransactionRef(string paymentReference)
        {
            if (string.IsNullOrWhiteSpace(paymentReference)) return "#TX-000000";
            return $"#TX-{paymentReference.Replace("_", "").Substring(0, Math.Min(6, paymentReference.Replace("_", "").Length)).ToUpper()}";
        }

        private static string FormatWorkerId(Guid id, DateTime? dateCreated)
        {
            var seed = id != Guid.Empty ? id.ToString("N").Substring(0, 4).ToUpper() : (dateCreated?.Ticks.ToString() ?? "0000");
            return $"#WK-{seed}";
        }

        private static string GetInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "CC";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        private static string MaskAccount(string? accountNumber, string bankName)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return string.IsNullOrWhiteSpace(bankName) ? "No bank details" : bankName;
            var tail = accountNumber.Length >= 4 ? accountNumber[^4..] : accountNumber;
            var bank = string.IsNullOrWhiteSpace(bankName) ? "" : $"{bankName} ";
            return $"{bank}••••{tail}";
        }

        private static string MapBookingStatus(BookingStatus status) => status switch
        {
            BookingStatus.Completed => "Completed",
            BookingStatus.InProgress or BookingStatus.AwaitingClientConfirmation or BookingStatus.AwaitingClientStartConfirmation => "In Progress",
            BookingStatus.Rejected or BookingStatus.Cancelled or BookingStatus.Expired => "Cancelled",
            _ => "Pending"
        };

        private static string MapEscrowStatus(EscrowStatus status) => status switch
        {
            EscrowStatus.Held => "In Escrow",
            EscrowStatus.Released or EscrowStatus.PaidOut => "Released",
            EscrowStatus.Refunded => "Refunded",
            EscrowStatus.Disputed => "Disputed",
            _ => "In Escrow"
        };

        private static string MapGateway(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod)) return "Paystack Card";
            if (paymentMethod.Contains("Bank", StringComparison.OrdinalIgnoreCase)) return "Bank Transfer";
            return "Paystack Card";
        }

        private static List<AuditActivityDto> BuildAuditActivity(
            List<Booking> bookings,
            List<Payment> payments,
            List<Ratings> ratings,
            DateTime now)
        {
            var bookingById = bookings.ToDictionary(b => b.Id);
            var activities = new List<(DateTime When, AuditActivityDto Activity)>();

            foreach (var booking in bookings)
            {
                var bookingRef = FormatBookingId(booking.Id, booking.DateCreated);
                var client = booking.Client?.FullName?.ToString() ?? "Client";
                var service = booking.ServiceType?.Name ?? "Cleaning";
                activities.Add((booking.DateCreated, new AuditActivityDto
                {
                    IconVariant = "sky",
                    Title = $"New booking created — {bookingRef}",
                    Description = $"{client} • {service}",
                    TimeAgo = FormatTimeAgo(booking.DateCreated, now)
                }));

                if (booking.BookingStatus == BookingStatus.Completed)
                {
                    var completedAt = booking.DateModified ?? booking.DateCreated;
                    activities.Add((completedAt, new AuditActivityDto
                    {
                        IconVariant = "emerald",
                        Title = $"Booking completed — {bookingRef}",
                        Description = $"{client} • {service}",
                        TimeAgo = FormatTimeAgo(completedAt, now)
                    }));
                }
            }

            foreach (var payment in payments.Where(p => !p.IsDeleted && p.Status == PaymentStatus.Successful))
            {
                if (!bookingById.TryGetValue(payment.BookingId, out var booking))
                    continue;

                var paidAt = payment.DateModified ?? payment.DateCreated;
                activities.Add((paidAt, new AuditActivityDto
                {
                    IconVariant = "amber",
                    Title = $"Payment received — {FormatBookingId(booking.Id, booking.DateCreated)}",
                    Description = $"{booking.Client?.FullName?.ToString() ?? "Client"} • {booking.ServiceType?.Name ?? "Cleaning"} • ₦{payment.Amount:N0}",
                    TimeAgo = FormatTimeAgo(paidAt, now)
                }));
            }

            foreach (var rating in ratings.Where(r => !r.IsDeleted))
            {
                if (!bookingById.TryGetValue(rating.BookingId, out var booking))
                    continue;

                activities.Add((rating.DateCreated, new AuditActivityDto
                {
                    IconVariant = "rose",
                    Title = $"Review received — {FormatBookingId(booking.Id, booking.DateCreated)}",
                    Description = $"{booking.Client?.FullName?.ToString() ?? "Client"} • {rating.RatingValue}/5",
                    TimeAgo = FormatTimeAgo(rating.DateCreated, now)
                }));
            }

            return activities
                .OrderByDescending(item => item.When)
                .Take(8)
                .Select(item => item.Activity)
                .ToList();
        }

        private static string FormatTimeAgo(DateTime date, DateTime now)
        {
            var diff = now - date;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} minutes ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
            return date.ToString("MMM d");
        }
    }
}
