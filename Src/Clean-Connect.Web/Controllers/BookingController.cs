using AspNetCoreHero.ToastNotification.Abstractions;
using Clean_Connect.Application.Command.BookingChatCommand;
using Clean_Connect.Application.Command.BookingCommand;
using Clean_Connect.Application.Command.ClientCommands;
using Clean_Connect.Application.Command.PaymentCommand;
using Clean_Connect.Application.Command.WorkerCommands;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Query.BookingChatQuery;
using Clean_Connect.Application.Query.ClientQuery;
using Clean_Connect.Application.Query.WorkersQuery;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Web.Models;
using Clean_Connect.Web.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Clean_Connect.Web.Controllers
{
    [Authorize]
    public class BookingController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _repo;
        private readonly INotyfService _notyf;
        private readonly IRealtimeNotificationService _notifications;
        private readonly IWorkerPresenceService _presence;
        private readonly ILogger<BookingController> _logger;
        private bool _currentWorkerSuspended;

        public BookingController(
            IMediator mediator,
            IUnitOfWork repo,
            INotyfService notyf,
            IRealtimeNotificationService notifications,
            IWorkerPresenceService presence,
            ILogger<BookingController> logger)
        {
            _mediator = mediator;
            _repo = repo;
            _notyf = notyf;
            _notifications = notifications;
            _presence = presence;
            _logger = logger;
        }

        [HttpGet("Create-Booking")]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                _notyf.Information("Please complete your client profile before creating a booking.");
                return RedirectToAction("CreateClientProfile", "Client");
            }

            var model = new CreateBookingViewModel();
            if (Guid.TryParse(Request.Query["workerId"], out var workerId)) model.WorkerId = workerId;
            if (Guid.TryParse(Request.Query["serviceTypeId"], out var serviceTypeId)) model.ServiceTypeId = serviceTypeId;
            if (DateTime.TryParse(Request.Query["dateOfService"], out var dateOfService)) model.DateOfService = dateOfService;
            if (!string.IsNullOrWhiteSpace(Request.Query["timeRange"])) model.TimeRange = Request.Query["timeRange"]!;
            if (DateTime.TryParse(Request.Query["startTime"], out var startTime)) model.StartTime = startTime;
            if (DateTime.TryParse(Request.Query["endTime"], out var endTime)) model.EndTime = endTime;
            if (double.TryParse(Request.Query["latitude"], out var latitude)) model.Latitude = latitude;
            if (double.TryParse(Request.Query["longitude"], out var longitude)) model.Longitude = longitude;
            if (double.TryParse(Request.Query["radius"], out var radius) && radius >= 100) model.RadiusInMeters = radius;

            await PopulateBookingOptionsAsync(ct);
            return View(model);
        }

        [HttpPost("Create-Booking")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBookingViewModel model, CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                _notyf.Information("Please complete your client profile before creating a booking.");
                return RedirectToAction("CreateClientProfile", "Client");
            }

            // [Required] on a non-nullable Guid never fails when the field is missing from
            // the posted form (it binds to Guid.Empty), so validate the empties explicitly.
            if (model.WorkerId == Guid.Empty)
            {
                ModelState.AddModelError(nameof(model.WorkerId), "Please select a cleaning professional.");
            }
            if (model.ServiceTypeId == Guid.Empty)
            {
                ModelState.AddModelError(nameof(model.ServiceTypeId), "Please choose a cleaning service.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateBookingOptionsAsync(ct);
                _notyf.Error("Please correct the highlighted booking details.");
                return View(model);
            }

            try
            {
                var worker = await _repo.Workers.GetWorkerById(model.WorkerId, ct);
                if (worker == null)
                {
                    ModelState.AddModelError(nameof(model.WorkerId), "Selected worker was not found.");
                    await PopulateBookingOptionsAsync(ct);
                    _notyf.Error("Selected worker was not found.");
                    return View(model);
                }

                if (worker.ServiceTypeId != Guid.Empty && worker.ServiceTypeId != model.ServiceTypeId)
                {
                    ModelState.AddModelError(nameof(model.ServiceTypeId), "The selected cleaning service does not match this cleaner.");
                    await PopulateBookingOptionsAsync(ct);
                    _notyf.Error("The selected cleaning service does not match this cleaner.");
                    return View(model);
                }

                var command = new CreateBookingCommand(
                    client.Id,
                    model.WorkerId,
                    model.Latitude,
                    model.Longitude,
                    model.RadiusInMeters,
                    model.ServiceTypeId,
                    model.DateOfService,
                    model.TimeRange,
                    model.StartTime,
                    model.EndTime,
                    model.CouponCode,
                    User.Identity?.Name);

                var bookingId = await _mediator.Send(command, ct);
                if (bookingId == Guid.Empty)
                {
                    _notyf.Error("Unable to create booking.");
                    await PopulateBookingOptionsAsync(ct);
                    return View(model);
                }

                await _notifications.NotifyUserAsync(
                    worker.Email?.Value,
                    "New booking request",
                    $"{client.FullName} requested {worker.ServiceType?.Name ?? "a cleaning service"}.",
                    "success",
                    Url.Action("WorkerDetails", "Booking", new { bookingId }));

                _notyf.Success("Booking request sent to the worker.");
                return RedirectToAction("Dashboard", "Client");
            }
            catch (FluentValidation.ValidationException vex)
            {
                _logger.LogError(vex, "Booking validation failed for worker {WorkerId}.", model.WorkerId);
                foreach (var failure in vex.Errors)
                {
                    ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
                }
                _notyf.Error("Please correct the highlighted booking details.");
                await PopulateBookingOptionsAsync(ct);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Booking creation failed.");
                _notyf.Error("We couldn't create your booking. Please try again.");
                await PopulateBookingOptionsAsync(ct);
                return View(model);
            }
        }

        [HttpGet("Choose-Worker")]
        public async Task<IActionResult> ChooseWorker(Guid? serviceTypeId, DateTime? dateOfService, string? timeRange, double? latitude, double? longitude, double? radiusInMeters, CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                _notyf.Information("Please complete your client profile before choosing a worker.");
                return RedirectToAction("CreateClientProfile", "Client");
            }

            var date = dateOfService ?? DateTime.Today.AddDays(1);
            var time = string.IsNullOrWhiteSpace(timeRange) ? "Morning" : timeRange;

            var originLat = latitude ?? client.Location?.Latitude;
            var originLng = longitude ?? client.Location?.Longitude;

            var items = await BuildWorkerChoicesAsync(client, serviceTypeId, date, time, originLat, originLng, radiusInMeters, ct);

            var model = await BuildChooseWorkerModel(serviceTypeId, date, time, radiusInMeters, items, ct);

            ViewBag.OriginLatitude = originLat?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ViewBag.OriginLongitude = originLng?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ViewBag.OriginRadius = radiusInMeters?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

            return View(model);
        }

        [HttpGet("Choose-Worker/Results")]
        public async Task<IActionResult> WorkerResults(Guid? serviceTypeId, DateTime? dateOfService, string? timeRange, double? latitude, double? longitude, double? radiusInMeters, CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                return Unauthorized();
            }

            var date = dateOfService ?? DateTime.Today.AddDays(1);
            var time = string.IsNullOrWhiteSpace(timeRange) ? "Morning" : timeRange;

            var originLat = latitude ?? client.Location?.Latitude;
            var originLng = longitude ?? client.Location?.Longitude;

            var items = await BuildWorkerChoicesAsync(client, serviceTypeId, date, time, originLat, originLng, radiusInMeters, ct);

            var model = await BuildChooseWorkerModel(serviceTypeId, date, time, radiusInMeters, items, ct);

            ViewBag.OriginLatitude = originLat?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ViewBag.OriginLongitude = originLng?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ViewBag.OriginRadius = radiusInMeters?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

            return PartialView("_WorkerResults", model);
        }

        private async Task<List<WorkerChoiceItem>> BuildWorkerChoicesAsync(Client client, Guid? serviceTypeId, DateTime date, string time, double? originLat, double? originLng, double? radiusInMeters, CancellationToken ct)
        {
            var candidates = await _repo.Workers.GetAvailableWorkersWithDistanceAsync(originLat, originLng, radiusInMeters, serviceTypeId, date, ct);

            return candidates
                .Select(w => new WorkerChoiceItem
                {
                    Id = w.Worker.Id,
                    Name = w.Worker.FullName.ToString(),
                    ServiceTypeId = w.Worker.ServiceTypeId,
                    ServiceName = w.Worker.ServiceType?.Name ?? "Cleaning service",
                    Rating = w.Worker.AverageRating,
                    TotalRating = w.Worker.TotalRating,
                    HourlyRate = w.Worker.HourlyRate,
                    State = w.Worker.State,
                    Age = w.Worker.Age,
                    DistanceInKm = w.DistanceInKm,
                    IsOnline = _presence.IsOnline(w.Worker.Email.Value)
                })
                .OrderBy(w => !w.IsOnline)
                .ThenBy(w => w.DistanceInKm)
                .ThenByDescending(w => w.Rating)
                .ToList();
        }

        private async Task<ChooseWorkerViewModel> BuildChooseWorkerModel(Guid? serviceTypeId, DateTime date, string time, double? radiusInMeters, List<WorkerChoiceItem> items, CancellationToken ct)
        {
            var serviceTypes = (await _repo.ServiceTypes.GetAllAsync(ct)).ToList();

            return new ChooseWorkerViewModel
            {
                SelectedServiceTypeId = serviceTypeId,
                DateOfService = date,
                TimeRange = time,
                SelectedRadiusInMeters = radiusInMeters.HasValue && radiusInMeters.Value > 0 ? radiusInMeters : null,
                Workers = items,
                ServiceTypes = new SelectList(serviceTypes, "Id", "Name"),
                TimeRanges = Enum.GetNames<TimeRange>().Select(x => new SelectListItem(x, x)).ToList()
            };
        }

        [HttpGet("Worker-Bookings")]
        public async Task<IActionResult> Responses(CancellationToken ct)
        {
            var worker = await GetCurrentWorkerAsync(ct);
            if (worker == null)
            {
                if (_currentWorkerSuspended) return RedirectToAction("Dashboard", "Worker");
                _notyf.Information("Please complete your worker profile before responding to bookings.");
                return RedirectToAction("CreateWorkerProfile", "Worker");
            }

            var bookings = await _mediator.Send(new GetAllWorkerBookingsQuery(worker.Id), ct);
            return View(bookings.OrderByDescending(x => x.BookingDate).ToList());
        }

        [HttpGet("Worker-Booking-Details/{bookingId:guid}")]
        public async Task<IActionResult> WorkerDetails(Guid bookingId, CancellationToken ct)
        {
            var worker = await GetCurrentWorkerAsync(ct);
            if (worker == null)
            {
                if (_currentWorkerSuspended) return RedirectToAction("Dashboard", "Worker");
                _notyf.Information("Please complete your worker profile before viewing a booking.");
                return RedirectToAction("CreateWorkerProfile", "Worker");
            }

            var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
            if (booking == null || booking.WorkerId != worker.Id)
            {
                _notyf.Error("Booking was not found for your worker profile.");
                return RedirectToAction(nameof(Responses));
            }

            var dto = new Clean_Connect.Application.DTO.BookingDto
            {
                Id = booking.Id,
                ServiceName = booking.ServiceType?.Name ?? "Cleaning service",
                ClientName = booking.Client?.FullName?.ToString() ?? "Client",
                WorkersName = worker.FullName.ToString(),
                Rating = booking.Ratings?.RatingValue ?? 0,
                BookingDate = booking.DateOfBooking,
                DateOfService = booking.DateOfService,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                HourlyRate = booking.HourlyRate,
                TotalAmount = booking.TotalAmount,
                TimeRange = booking.TimeRange.ToString(),
                Amount = booking.Amount,
                Address = booking.Address,
                PaymentStatus = booking.PaymentStatus.ToString(),
                BookingStatus = booking.BookingStatus.ToString(),
                RejectionReason = booking.RejectionReason
            };

            return View(dto);
        }

        [HttpGet("Client-Bookings")]
        public async Task<IActionResult> ClientResponses(CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                _notyf.Information("Please complete your client profile before viewing booking responses.");
                return RedirectToAction("CreateClientProfile", "Client");
            }

            var bookings = await _mediator.Send(new GetAllClientBookingsQuery(client.Id), ct);
            return View(bookings.OrderByDescending(x => x.BookingDate).ToList());
        }

        [HttpGet("Client-Booking-Details/{bookingId:guid}")]
        public async Task<IActionResult> Details(Guid bookingId, CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                _notyf.Information("Please complete your client profile before viewing a booking.");
                return RedirectToAction("CreateClientProfile", "Client");
            }

            var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
            if (booking == null || booking.ClientId != client.Id)
            {
                _notyf.Error("Booking was not found for your client profile.");
                return RedirectToAction(nameof(ClientResponses));
            }

            var dto = new Clean_Connect.Application.DTO.BookingDto
            {
                Id = booking.Id,
                ServiceName = booking.ServiceType?.Name ?? "Cleaning service",
                ClientName = client.FullName,
                WorkersName = booking.Worker?.FullName ?? "Worker",
                BookingDate = booking.DateOfBooking,
                DateOfService = booking.DateOfService,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                HourlyRate = booking.HourlyRate,
                TotalAmount = booking.TotalAmount,
                TimeRange = booking.TimeRange.ToString(),
                Amount = booking.Amount,
                Address = booking.Address,
                PaymentStatus = booking.PaymentStatus.ToString(),
                BookingStatus = booking.BookingStatus.ToString(),
                RejectionReason = booking.RejectionReason,
                HasRating = await _repo.Ratings.ExistAsync(bookingId, ct)
            };

            return View(dto);
        }

        [HttpGet("Messages")]
        public async Task<IActionResult> Messages(CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client != null)
            {
                var bookings = await _mediator.Send(new GetAllClientBookingsQuery(client.Id), ct);
                var bookingIds = bookings.Select(x => x.Id).ToList();
                var chats = await _repo.BookingChats.GetByBookingIdsAsync(bookingIds, ct);

                var model = new BookingMessagesIndexViewModel
                {
                    ParticipantRole = "Client",
                    Threads = bookings
                        .Select(x =>
                        {
                            chats.TryGetValue(x.Id, out var chat);
                            return new BookingMessageThreadItem
                            {
                                BookingId = x.Id,
                                ServiceName = x.ServiceName,
                                OtherParticipantName = x.WorkersName,
                                BookingStatus = x.BookingStatus,
                                PaymentStatus = x.PaymentStatus,
                                DateOfService = x.DateOfService,
                                BookingDate = x.BookingDate,
                                TimeRange = x.TimeRange ?? string.Empty,
                                Address = x.Address ?? string.Empty,
                                Amount = x.Amount,
                                RejectionReason = x.RejectionReason,
                                UnreadCount = chat?.ClientUnreadCount ?? 0,
                                LatestMessageText = chat?.LastMessageText,
                                LatestMessageTime = chat?.LastMessageSentAt
                            };
                        })
                        .OrderByDescending(x => x.LatestMessageTime ?? x.BookingDate)
                        .ToList()
                };

                var latestThread = model.Threads
                    .OrderByDescending(x => x.LatestMessageTime ?? x.BookingDate)
                    .FirstOrDefault();
                if (latestThread != null)
                {
                    return RedirectToAction(nameof(Chat), new { bookingId = latestThread.BookingId, markRead = false });
                }

                return View(model);
            }

            var worker = await GetCurrentWorkerAsync(ct);
            if (worker != null)
            {
                var bookings = await _mediator.Send(new GetAllWorkerBookingsQuery(worker.Id), ct);
                var bookingIds = bookings.Select(x => x.Id).ToList();
                var chats = await _repo.BookingChats.GetByBookingIdsAsync(bookingIds, ct);

                var model = new BookingMessagesIndexViewModel
                {
                    ParticipantRole = "Worker",
                    Threads = bookings
                        .Select(x =>
                        {
                            chats.TryGetValue(x.Id, out var chat);
                            return new BookingMessageThreadItem
                            {
                                BookingId = x.Id,
                                ServiceName = x.ServiceName,
                                OtherParticipantName = x.ClientName,
                                BookingStatus = x.BookingStatus,
                                PaymentStatus = x.PaymentStatus,
                                DateOfService = x.DateOfService,
                                BookingDate = x.BookingDate,
                                TimeRange = x.TimeRange ?? string.Empty,
                                Address = x.Address ?? string.Empty,
                                Amount = x.Amount,
                                RejectionReason = x.RejectionReason,
                                UnreadCount = chat?.WorkerUnreadCount ?? 0,
                                LatestMessageText = chat?.LastMessageText,
                                LatestMessageTime = chat?.LastMessageSentAt
                            };
                        })
                        .OrderByDescending(x => x.LatestMessageTime ?? x.BookingDate)
                        .ToList()
                };

                var latestThread = model.Threads
                    .OrderByDescending(x => x.LatestMessageTime ?? x.BookingDate)
                    .FirstOrDefault();
                if (latestThread != null)
                {
                    return RedirectToAction(nameof(Chat), new { bookingId = latestThread.BookingId, markRead = false });
                }

                return View(model);
            }

            _notyf.Information("Please complete your profile before viewing messages.");
            return RedirectToAction("Index", "Home");
        }

#if DEBUG
        [AllowAnonymous]
        [HttpGet("Messages/Preview")]
        public IActionResult MessagesPreview()
        {
            var model = new BookingMessagesIndexViewModel
            {
                ParticipantRole = "Client",
                Threads = new List<BookingMessageThreadItem>
                {
                    new()
                    {
                        BookingId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        ServiceName = "Deep Cleaning",
                        OtherParticipantName = "Sarah Jenkins",
                        BookingStatus = "MarkAsPaid",
                        PaymentStatus = "Successful",
                        DateOfService = DateTime.UtcNow.AddDays(2),
                        BookingDate = DateTime.UtcNow.AddHours(-2),
                        TimeRange = "Morning (09:00 - 12:00)",
                        Address = "14 Admiralty Way, Lekki Phase 1"
                    },
                    new()
                    {
                        BookingId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        ServiceName = "Residential Cleaning",
                        OtherParticipantName = "David Adeleke",
                        BookingStatus = "InProgress",
                        PaymentStatus = "Successful",
                        DateOfService = DateTime.UtcNow,
                        BookingDate = DateTime.UtcNow.AddHours(-5),
                        TimeRange = "Afternoon (13:00 - 16:00)",
                        Address = "8 Glover Road, Ikoyi"
                    },
                    new()
                    {
                        BookingId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                        ServiceName = "Carpet Cleaning",
                        OtherParticipantName = "Amina Bello",
                        BookingStatus = "Completed",
                        PaymentStatus = "Successful",
                        DateOfService = DateTime.UtcNow.AddDays(-3),
                        BookingDate = DateTime.UtcNow.AddDays(-4),
                        TimeRange = "Morning (10:00 - 13:00)",
                        Address = "22 Victoria Island, Lagos"
                    }
                }
            };

            return View("Messages", model);
        }

        [AllowAnonymous]
        [HttpGet("Booking/Preview/Chat")]
        public IActionResult ChatPreview()
        {
            var model = new BookingChatPageViewModel
            {
                BookingId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ServiceName = "Deep Cleaning",
                OtherParticipantName = "Sarah Jenkins",
                ParticipantRole = "Client",
                ConversationId = "preview-conv-123",
                BookingStatus = "MarkAsPaid",
                PaymentStatus = "Successful",
                DateOfService = DateTime.UtcNow.AddDays(2),
                TimeRange = "Morning (09:00 - 12:00)",
                Address = "14 Admiralty Way, Lekki Phase 1",
                SessionUrl = "/Booking/Preview/chat/session"
            };

            return View("Chat", model);
        }
#endif

        [HttpGet("Booking/{bookingId:guid}/Messages")]
        public async Task<IActionResult> Chat(Guid bookingId, bool markRead = true, CancellationToken ct = default)
        {
            try
            {
                var chat = await _mediator.Send(new GetBookingChatDetailsQuery(bookingId), ct);
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct)
                    ?? throw new KeyNotFoundException($"Booking with ID {bookingId} not found.");

                try
                {
                    if (markRead)
                    {
                        await _mediator.Send(new MarkConversationReadCommand(bookingId), ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to mark conversation read on opening for booking {BookingId}", bookingId);
                }

                var dto = new Clean_Connect.Application.DTO.BookingDto
                {
                    Id = booking.Id,
                    ServiceName = booking.ServiceType?.Name ?? "Cleaning service",
                    ClientName = booking.Client?.FullName?.ToString() ?? "Client",
                    WorkersName = booking.Worker?.FullName?.ToString() ?? "Worker",
                    BookingDate = booking.DateOfBooking,
                    DateOfService = booking.DateOfService,
                    TimeRange = booking.TimeRange.ToString(),
                    Amount = booking.Amount,
                    Address = booking.Address,
                    PaymentStatus = booking.PaymentStatus.ToString(),
                    BookingStatus = booking.BookingStatus.ToString()
                };

                var sessionUrl = Url.Action(nameof(ChatSession), "Booking", new { bookingId }) ?? string.Empty;
                var chatPage = BookingChatPageViewModel.From(dto, chat, sessionUrl) with
                {
                    Threads = await GetMessageThreadsAsync(chat.ParticipantRole, ct)
                };
                return View("Chat", chatPage);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Booking chat page access denied for booking {BookingId}.", bookingId);
                _notyf.Error("Only the booking client and assigned worker can access this chat.");
                return RedirectToAction(nameof(Messages));
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Booking chat page not found for booking {BookingId}.", bookingId);
                _notyf.Error("Booking chat was not found.");
                return RedirectToAction(nameof(Messages));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Booking chat page is not available for booking {BookingId}.", bookingId);
                _notyf.Error("Chat is not available right now.");
                return RedirectToAction(nameof(Messages));
            }
        }

        [HttpPost("Client-Bookings/{bookingId:guid}/rate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rate(Guid bookingId, int RatingValue, string? Comment, CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                _notyf.Information("Please complete your client profile before rating a worker.");
                return RedirectToAction("CreateClientProfile", "Client");
            }

            try
            {
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
                if (booking == null || booking.ClientId != client.Id)
                {
                    _notyf.Error("Booking was not found for your client profile.");
                    return RedirectToAction(nameof(ClientResponses));
                }

                if (booking.BookingStatus == BookingStatus.Expired)
                {
                    _notyf.Error("This booking has expired and can no longer be acted on.");
                    return RedirectToAction(nameof(Details), new { bookingId });
                }

                await _mediator.Send(new CreateRatingCommand(
                    booking.WorkerId,
                    client.Id,
                    bookingId,
                    RatingValue,
                    string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim(),
                    User.Identity?.Name), ct);

                _notyf.Success("Thanks for rating the worker.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rate booking {BookingId}", bookingId);
                _notyf.Error("We couldn't submit your review. Please try again.");
            }

            return RedirectToAction(nameof(Details), new { bookingId });
        }

        [HttpPost("Client-Bookings/{bookingId:guid}/pay")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(Guid bookingId, CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                _notyf.Information("Please complete your client profile before paying for a booking.");
                return RedirectToAction("CreateClientProfile", "Client");
            }

            try
            {
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
                if (booking == null || booking.ClientId != client.Id)
                {
                    _notyf.Error("Booking was not found for your client profile.");
                    return RedirectToAction(nameof(ClientResponses));
                }

                if (booking.BookingStatus == BookingStatus.Expired)
                {
                    _notyf.Error("This booking has expired and can no longer be paid for.");
                    return RedirectToAction(nameof(ClientResponses));
                }

                var callbackUrl = Url.Action("Callback", "Payment", new { }, Request.Scheme);

                var payment = await _mediator.Send(new PayForAcceptedBookingCommand(
                    bookingId,
                    client.Id,
                    client.Email.Value,
                    "Paystack",
                    User.Identity?.Name,
                    callbackUrl), ct);

                _notyf.Success("Payment checkout is ready.");
                return Redirect(payment.CheckoutUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment initialization failed for booking {BookingId}", bookingId);
                _notyf.Error("We couldn't start your payment. Please check your booking and try again.");
                return RedirectToAction(nameof(ClientResponses));
            }
        }

        [HttpPost("Booking/{bookingId:guid}/chat/session")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatSession(Guid bookingId, CancellationToken ct)
        {
            try
            {
                var chat = await _mediator.Send(new GetBookingChatDetailsQuery(bookingId), ct);
                return Json(chat);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Booking chat session could not be found for booking {BookingId}.", bookingId);
                return NotFound(new { message = "Booking chat was not found." });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Booking chat session access denied for booking {BookingId}.", bookingId);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Booking chat session is not available for booking {BookingId}.", bookingId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Chat is not available right now." });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Agora Chat request failed for booking {BookingId}.", bookingId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Chat is not available right now." });
            }
        }

        [HttpGet("Messages/UnreadCount")]
        [HttpGet("Booking/Messages/UnreadCount")]
        public async Task<IActionResult> UnreadMessageCount(CancellationToken ct)
        {
            var count = await _mediator.Send(new GetUnreadMessageCountQuery(), ct);
            return Json(new { count });
        }

        public record NotifyMessageSentRequest(string MessageText, string? MessageId = null);

        [HttpPost("Booking/{bookingId:guid}/messages/notify")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifyMessageSent(Guid bookingId, [FromForm] NotifyMessageSentRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request?.MessageText))
            {
                return BadRequest(new { message = "MessageText cannot be empty." });
            }

            try
            {
                var result = await _mediator.Send(new RecordMessageSentCommand(bookingId, request.MessageText, request.MessageId), ct);
                return Json(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to notify message for booking {BookingId}", bookingId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Booking not found for notification {BookingId}", bookingId);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record message notification for booking {BookingId}", bookingId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to record message notification." });
            }
        }

        [HttpPost("Booking/{bookingId:guid}/messages/read")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkMessagesRead(Guid bookingId, CancellationToken ct)
        {
            try
            {
                var remaining = await _mediator.Send(new MarkConversationReadCommand(bookingId), ct);
                return Json(new { success = true, count = remaining });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark messages read for booking {BookingId}", bookingId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to mark messages read." });
            }
        }

        [HttpPost("Worker-Bookings/{bookingId:guid}/accept")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(Guid bookingId, CancellationToken ct)
        {
            return await RespondToBookingAsync(bookingId, accepted: true, ct);
        }

        [HttpPost("Worker-Bookings/{bookingId:guid}/reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(Guid bookingId, string? rejectionReason, CancellationToken ct)
        {
            return await RespondToBookingAsync(bookingId, accepted: false, ct, rejectionReason);
        }

        [HttpPost("Worker-Bookings/{bookingId:guid}/start-job")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartJob(Guid bookingId, CancellationToken ct)
        {
            var worker = await GetCurrentWorkerAsync(ct);
            if (worker == null)
            {
                if (_currentWorkerSuspended) return RedirectToAction("Dashboard", "Worker");
                _notyf.Information("Please complete your worker profile before starting a job.");
                return RedirectToAction("CreateWorkerProfile", "Worker");
            }

            try
            {
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
                if (booking == null || booking.WorkerId != worker.Id)
                {
                    _notyf.Error("Booking was not found for your worker profile.");
                    return RedirectToAction(nameof(WorkerDetails), new { bookingId });
                }

                if (booking.BookingStatus == BookingStatus.Expired)
                {
                    _notyf.Error("This booking has expired and can no longer be started.");
                    return RedirectToAction(nameof(WorkerDetails), new { bookingId });
                }

                if (booking.DateOfService.Date != DateTime.UtcNow.Date)
                {
                    _notyf.Error("You can only request to start this job on its scheduled service date. Please try again on that day.");
                    return RedirectToAction(nameof(WorkerDetails), new { bookingId });
                }

                await _mediator.Send(new RequestJobStartCommand(worker.Id, bookingId), ct);
                _notyf.Success("Start request sent. The client will confirm the job is in progress.");

                await _notifications.NotifyUserAsync(
                    booking?.Client?.Email?.Value,
                    "Job start requested",
                    $"{worker.FullName} has arrived and says the job is starting. Please confirm on your bookings page so it is marked as in progress.",
                    "info",
                    Url.Action("Details", "Booking", new { bookingId }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request job start for booking {BookingId}", bookingId);
                _notyf.Error("We couldn't send the job start request. Please try again.");
            }

            return RedirectToAction(nameof(WorkerDetails), new { bookingId });
        }

        [HttpPost("Worker-Bookings/{bookingId:guid}/complete-job")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteJob(Guid bookingId, CancellationToken ct)
        {
            var worker = await GetCurrentWorkerAsync(ct);
            if (worker == null)
            {
                if (_currentWorkerSuspended) return RedirectToAction("Dashboard", "Worker");
                _notyf.Information("Please complete your worker profile before completing a job.");
                return RedirectToAction("CreateWorkerProfile", "Worker");
            }

            try
            {
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
                if (booking == null || booking.WorkerId != worker.Id)
                {
                    _notyf.Error("Booking was not found for your worker profile.");
                    return RedirectToAction(nameof(WorkerDetails), new { bookingId });
                }

                if (booking.BookingStatus == BookingStatus.Expired)
                {
                    _notyf.Error("This booking has expired and can no longer be completed.");
                    return RedirectToAction(nameof(WorkerDetails), new { bookingId });
                }

                await _mediator.Send(new JobCompletedByWorkerCommand(bookingId, worker.Id), ct);
                _notyf.Success("Job marked as done. Awaiting client confirmation.");

                await _notifications.NotifyUserAsync(
                    booking?.Client?.Email?.Value,
                    "Job completed",
                    $"{worker.FullName} says your cleaning job is done. Please confirm so your payment is released.",
                    "success",
                    Url.Action("Details", "Booking", new { bookingId }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete job for booking {BookingId}", bookingId);
                _notyf.Error("We couldn't mark the job as complete. Please try again.");
            }

            return RedirectToAction(nameof(WorkerDetails), new { bookingId });
        }

        [HttpPost("Client-Bookings/{bookingId:guid}/confirm-completion")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCompletion(Guid bookingId, CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                _notyf.Information("Please complete your client profile before confirming a job.");
                return RedirectToAction("CreateClientProfile", "Client");
            }

            try
            {
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
                if (booking == null || booking.ClientId != client.Id)
                {
                    _notyf.Error("Booking was not found for your client profile.");
                    return RedirectToAction(nameof(Details), new { bookingId });
                }

                if (booking.BookingStatus == BookingStatus.Expired)
                {
                    _notyf.Error("This booking has expired and can no longer be confirmed as completed.");
                    return RedirectToAction(nameof(Details), new { bookingId });
                }

                await _mediator.Send(new MarkAsCompletedCommand(bookingId, client.Id), ct);
                _notyf.Success("Booking confirmed as completed. The worker has been paid.");

                await _notifications.NotifyUserAsync(
                    booking?.Worker?.Email?.Value,
                    "Job confirmed",
                    $"{client.FullName} confirmed your job is complete. Payment has been released to you.",
                    "success",
                    Url.Action("WorkerDetails", "Booking", new { bookingId }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm completion for booking {BookingId}", bookingId);
                _notyf.Error("We couldn't confirm the completed job. Please try again.");
            }

            return RedirectToAction(nameof(Details), new { bookingId });
        }

        [HttpPost("Client-Bookings/{bookingId:guid}/confirm-start")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmStart(Guid bookingId, CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                _notyf.Information("Please complete your client profile before confirming a job.");
                return RedirectToAction("CreateClientProfile", "Client");
            }

            try
            {
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
                if (booking == null || booking.ClientId != client.Id)
                {
                    _notyf.Error("Booking was not found for your client profile.");
                    return RedirectToAction(nameof(Details), new { bookingId });
                }

                if (booking.BookingStatus == BookingStatus.Expired)
                {
                    _notyf.Error("This booking has expired and can no longer be confirmed as in progress.");
                    return RedirectToAction(nameof(Details), new { bookingId });
                }

                await _mediator.Send(new ConfirmJobStartedCommand(bookingId, client.Id), ct);
                _notyf.Success("Job confirmed as in progress. The worker has been notified.");

                await _notifications.NotifyUserAsync(
                    booking?.Worker?.Email?.Value,
                    "Job in progress",
                    $"{client.FullName} confirmed your job is in progress. Track the booking until it is done.",
                    "success",
                    Url.Action("WorkerDetails", "Booking", new { bookingId }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm job start for booking {BookingId}", bookingId);
                _notyf.Error("We couldn't confirm that the job has started. Please try again.");
            }

            return RedirectToAction(nameof(Details), new { bookingId });
        }

        private async Task<IActionResult> RespondToBookingAsync(Guid bookingId, bool accepted, CancellationToken ct, string? rejectionReason = null)
        {
            var worker = await GetCurrentWorkerAsync(ct);
            if (worker == null)
            {
                if (_currentWorkerSuspended) return RedirectToAction("Dashboard", "Worker");
                _notyf.Information("Please complete your worker profile before responding to bookings.");
                return RedirectToAction("CreateWorkerProfile", "Worker");
            }

            try
            {
                var booking = await _repo.Bookings.GetBookingById(bookingId, ct);
                if (booking == null || booking.WorkerId != worker.Id)
                {
                    _notyf.Error("Booking was not found for your worker profile.");
                    return RedirectToAction(nameof(Responses));
                }

                if (booking.BookingStatus == BookingStatus.Expired)
                {
                    _notyf.Error("This booking has expired and can no longer be accepted or declined.");
                    return RedirectToAction(nameof(Responses));
                }

                if (accepted)
                {
                    await _mediator.Send(new AcceptBookingCommand(bookingId, worker.Id), ct);
                    _notyf.Success("Booking accepted. The client has been notified.");
                    await _notifications.NotifyUserAsync(
                        booking.Client?.Email?.Value,
                        "Booking accepted",
                        $"{worker.FullName} accepted your booking. You can proceed with payment.",
                        "success",
                        Url.Action("Details", "Booking", new { bookingId = booking.Id }));
                }
                else
                {
                    await _mediator.Send(new RejectBookingCommand(bookingId, worker.Id, rejectionReason), ct);
                    _notyf.Success("Booking rejected. The client has been notified.");
                    await _notifications.NotifyUserAsync(
                        booking.Client?.Email?.Value,
                        "Booking declined",
                        $"{worker.FullName} declined your booking request: {rejectionReason}",
                        "warning",
                        Url.Action("Details", "Booking", new { bookingId = booking.Id }));
                }

                return RedirectToAction(nameof(Responses));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker booking response failed for {BookingId}", bookingId);
                _notyf.Error("We couldn't update your booking response. Please try again.");
                return RedirectToAction(nameof(Responses));
            }
        }

        private async Task PopulateBookingOptionsAsync(CancellationToken ct)
        {
            var workers = (await _repo.Workers.GetAllWorkers(ct)).ToList();
            var serviceTypes = (await _repo.ServiceTypes.GetAllAsync(ct)).ToList();

            ViewBag.ServiceTypes = new SelectList(serviceTypes, "Id", "Name");
            ViewBag.Workers = workers
                .Select(worker => new SelectListItem
                {
                    Value = worker.Id.ToString(),
                    Text = $"{worker.FullName} - {worker.ServiceType?.Name ?? "Cleaning service"}",
                    Group = new SelectListGroup { Name = worker.ServiceType?.Name ?? "Other services" }
                })
                .ToList();
            ViewBag.WorkerCards = workers.Select(worker => new
            {
                worker.Id,
                Name = worker.FullName.ToString(),
                ServiceTypeId = worker.ServiceTypeId,
                ServiceName = worker.ServiceType?.Name ?? "Cleaning service",
                HourlyRate = worker.HourlyRate,
                Rating = worker.AverageRating,
                State = worker.State
            }).ToList();
            ViewBag.TimeRanges = Enum.GetNames<TimeRange>().Select(x => new SelectListItem(x, x)).ToList();
        }

        private async Task<Client?> GetCurrentClientAsync(CancellationToken ct)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email)) return null;
            var client = await _repo.Clients.GetByEmail(email, ct);
            if (client?.IsSuspended == true)
            {
                _notyf.Error("Your account is suspended. Booking and service access is restricted. Contact support for assistance.");
                return null;
            }
            return client;
        }

        private async Task<Worker?> GetCurrentWorkerAsync(CancellationToken ct)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email)) return null;
            var worker = await _repo.Workers.GetByEmail(email, ct);
            if (worker?.IsSuspended == true)
            {
                _currentWorkerSuspended = true;
                _notyf.Error("Your account is suspended. Worker services and booking actions are restricted. Contact support for assistance.");
                return null;
            }
            return worker;
        }

        private async Task<IReadOnlyList<BookingMessageThreadItem>> GetMessageThreadsAsync(string participantRole, CancellationToken ct)
        {
            if (string.Equals(participantRole, "Worker", StringComparison.OrdinalIgnoreCase))
            {
                var worker = await GetCurrentWorkerAsync(ct);
                if (worker == null) return Array.Empty<BookingMessageThreadItem>();

                var bookings = await _mediator.Send(new GetAllWorkerBookingsQuery(worker.Id), ct);
                var chats = await _repo.BookingChats.GetByBookingIdsAsync(bookings.Select(x => x.Id).ToList(), ct);
                return bookings.Select(booking =>
                {
                    chats.TryGetValue(booking.Id, out var chat);
                    return new BookingMessageThreadItem
                    {
                        BookingId = booking.Id,
                        ServiceName = booking.ServiceName,
                        OtherParticipantName = booking.ClientName,
                        BookingStatus = booking.BookingStatus,
                        PaymentStatus = booking.PaymentStatus,
                        DateOfService = booking.DateOfService,
                        BookingDate = booking.BookingDate,
                        TimeRange = booking.TimeRange ?? string.Empty,
                        Address = booking.Address ?? string.Empty,
                        Amount = booking.Amount,
                        RejectionReason = booking.RejectionReason,
                        UnreadCount = chat?.WorkerUnreadCount ?? 0,
                        LatestMessageText = chat?.LastMessageText,
                        LatestMessageTime = chat?.LastMessageSentAt
                    };
                }).OrderByDescending(x => x.LatestMessageTime ?? x.BookingDate).ToList();
            }

            var client = await GetCurrentClientAsync(ct);
            if (client == null) return Array.Empty<BookingMessageThreadItem>();

            var clientBookings = await _mediator.Send(new GetAllClientBookingsQuery(client.Id), ct);
            var clientChats = await _repo.BookingChats.GetByBookingIdsAsync(clientBookings.Select(x => x.Id).ToList(), ct);
            return clientBookings.Select(booking =>
            {
                clientChats.TryGetValue(booking.Id, out var chat);
                return new BookingMessageThreadItem
                {
                    BookingId = booking.Id,
                    ServiceName = booking.ServiceName,
                    OtherParticipantName = booking.WorkersName,
                    BookingStatus = booking.BookingStatus,
                    PaymentStatus = booking.PaymentStatus,
                    DateOfService = booking.DateOfService,
                    BookingDate = booking.BookingDate,
                    TimeRange = booking.TimeRange ?? string.Empty,
                    Address = booking.Address ?? string.Empty,
                    Amount = booking.Amount,
                    RejectionReason = booking.RejectionReason,
                    UnreadCount = chat?.ClientUnreadCount ?? 0,
                    LatestMessageText = chat?.LastMessageText,
                    LatestMessageTime = chat?.LastMessageSentAt
                };
            }).OrderByDescending(x => x.LatestMessageTime ?? x.BookingDate).ToList();
        }
    }
}
