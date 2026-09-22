using AspNetCoreHero.ToastNotification.Abstractions;
using Clean_Connect.Application.Command.PaymentCommand;
using Clean_Connect.Application.Command.WorkerCommands;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Application.Query.WorkersQuery;
using Clean_Connect.Web.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Clean_Connect.Web.Controllers
{
    [Authorize]
    public class WorkerController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _repo;
        private readonly INotyfService _notyf;
        private readonly ILogger<WorkerController> _logger;
        private readonly IPaystackService _paystackService;

        public WorkerController(IMediator mediator, IUnitOfWork repo, INotyfService notyf, ILogger<WorkerController> logger, IPaystackService paystackService)
        {
            _mediator = mediator;
            _repo = repo;
            _notyf = notyf;
            _logger = logger;
            _paystackService = paystackService;
        }

        [HttpGet("Create-Worker-Profile")]
        public async Task<IActionResult> CreateWorkerProfile(CancellationToken ct)
        {
            var serviceTypes = await _repo.ServiceTypes.GetAllAsync(ct);
            ViewBag.ServiceType = new SelectList(serviceTypes, "Id", "Name");
            return View();
        }

        [HttpPost("Create-Worker-Profile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWorkerProfile(CreateWorkerCommand command, CancellationToken ct)
        {
            // Validation failed
            if (!ModelState.IsValid)
            {
                var serviceTypes = await _repo.ServiceTypes.GetAllAsync(ct);
                ViewBag.ServiceType = new SelectList(serviceTypes, "Id", "Name");

                _notyf.Error("Please correct the highlighted errors.");

                return View("CreateWorkerProfile", command);
            }

            try
            {
                var result = await _mediator.Send(command, ct);

                if (result)
                {
                    _notyf.Success("Worker profile created successfully.");

                    return RedirectToAction("Dashboard");
                }

                _notyf.Error("Unable to create worker profile.");

                var st = await _repo.ServiceTypes.GetAllAsync(ct);
                ViewBag.ServiceType = new SelectList(st, "Id", "Name");

                return View("CreateWorkerProfile", command);
            }
            catch (FluentValidation.ValidationException vex)
            {
                _logger.LogWarning(vex, "Worker profile creation validation failed.");

                _notyf.Error(vex.Message);

                var st2 = await _repo.ServiceTypes.GetAllAsync(ct);
                ViewBag.ServiceType = new SelectList(st2, "Id", "Name");

                return View("CreateWorkerProfile", command);
            }
            catch (UnauthorizedAccessException uex)
            {
                _logger.LogWarning(uex, "Worker profile creation unauthorized.");

                _notyf.Error(uex.Message);

                return RedirectToAction("Login", "Auth");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker profile creation failed.");

                _notyf.Error($"Something went wrong: {ex.Message}");

                var st3 = await _repo.ServiceTypes.GetAllAsync(ct);
                ViewBag.ServiceType = new SelectList(st3, "Id", "Name");

                return View("CreateWorkerProfile", command);
            }
        }

        [HttpGet("Worker-Dashboard")]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            try
            {
                var email = User.Identity?.Name;
                if (!string.IsNullOrEmpty(email))
                {
                    var existingWorker = await _repo.Workers.GetByEmail(email, ct);
                    if (existingWorker == null)
                    {
                        _notyf.Information("Please complete your profile first.");
                        return RedirectToAction(nameof(CreateWorkerProfile));
                    }
                }

                var model = await _mediator.Send(new GetWorkerDashboardQuery(), ct);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load worker dashboard");
                _notyf.Error("Unable to load dashboard data.");
                return RedirectToAction(nameof(CreateWorkerProfile));
            }
        }

        [HttpGet("Update-Worker-Details")]
        public async Task<IActionResult> UpdateDetails(CancellationToken ct)
        {
            try
            {
                var email = User.Identity?.Name;
                if (string.IsNullOrEmpty(email))
                {
                    _notyf.Error("User not authenticated.");
                    return RedirectToAction("Login", "Auth");
                }

                var worker = await _repo.Workers.GetByEmail(email, ct);
                if (worker == null)
                {
                    _notyf.Error("Worker not found.");
                    return RedirectToAction("Dashboard");
                }

                var serviceTypes = await _repo.ServiceTypes.GetAllAsync(ct);
                ViewBag.ServiceType = new SelectList(serviceTypes, "Id", "Name", worker.ServiceTypeId);

                var command = new UpdateWorkerCommand(
                    worker.Id,
                    worker.FullName?.FirstName ?? "",
                    worker.FullName?.LastName ?? "",
                    worker.Location?.Latitude ?? 8.5,
                    worker.Location?.Longitude ?? 6.5,
                    worker.Address?.Value ?? "",
                    worker.Contact?.Value ?? "",
                    worker.ServiceTypeId,
                    worker.Email?.Value ?? "",
                    worker.Gender.ToString(),
                    worker.State ?? "",
                    worker.DateOfBirth);

                return View(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load worker details for update.");
                _notyf.Error("Unable to load worker details.");
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost("Update-Worker-Details")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDetails(UpdateWorkerCommand command, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                var st = await _repo.ServiceTypes.GetAllAsync(ct);
                ViewBag.ServiceType = new SelectList(st, "Id", "Name", command.ServiceTypeId);
                _notyf.Error("Please correct the highlighted errors.");
                return View(command);
            }

            try
            {
                var result = await _mediator.Send(command, ct);

                if (result)
                {
                    _notyf.Success("Worker details updated successfully.");
                    return RedirectToAction("Dashboard");
                }

                _notyf.Error("Unable to update worker details.");
                return View(command);
            }
            catch (FluentValidation.ValidationException vex)
            {
                _logger.LogWarning(vex, "Worker update validation failed.");
                _notyf.Error(vex.Message);
                var st2 = await _repo.ServiceTypes.GetAllAsync(ct);
                ViewBag.ServiceType = new SelectList(st2, "Id", "Name", command.ServiceTypeId);
                return View(command);
            }
            catch (KeyNotFoundException)
            {
                _notyf.Error("Worker not found.");
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker update failed.");
                _notyf.Error($"Something went wrong: {ex.Message}");
                var st3 = await _repo.ServiceTypes.GetAllAsync(ct);
                ViewBag.ServiceType = new SelectList(st3, "Id", "Name", command.ServiceTypeId);
                return View(command);
            }
        }

        [HttpGet("Withdraw")]
        public async Task<IActionResult> Withdraw(CancellationToken ct)
        {
            try
            {
                var email = User.Identity?.Name;
                if (string.IsNullOrEmpty(email))
                {
                    _notyf.Error("User not authenticated.");
                    return RedirectToAction("Login", "Auth");
                }

                var worker = await _repo.Workers.GetByEmail(email, ct);
                if (worker == null)
                {
                    _notyf.Error("Worker not found.");
                    return RedirectToAction("Dashboard");
                }

                var wallet = await _repo.Wallets.GetByWorkerId(worker.Id, ct);
                var model = new WithdrawViewModel { WalletBalance = wallet?.Balance ?? 0 };

                await LoadBankListAsync(ct);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load withdraw page.");
                _notyf.Error("Unable to load withdraw page.");
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost("Withdraw")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(WithdrawViewModel model, CancellationToken ct)
        {
            try
            {
                var email = User.Identity?.Name;
                if (string.IsNullOrEmpty(email))
                {
                    _notyf.Error("User not authenticated.");
                    return RedirectToAction("Login", "Auth");
                }

                var worker = await _repo.Workers.GetByEmail(email, ct);
                if (worker == null)
                {
                    _notyf.Error("Worker not found.");
                    return RedirectToAction("Dashboard");
                }

                var wallet = await _repo.Wallets.GetByWorkerId(worker.Id, ct);
                model.WalletBalance = wallet?.Balance ?? 0;

                if (!ModelState.IsValid)
                {
                    await LoadBankListAsync(ct);
                    _notyf.Error("Please correct the highlighted errors.");
                    return View(model);
                }

                var command = new WithdrawFromWalletCommand(
                    worker.Id,
                    model.Amount,
                    model.AccountNumber ?? "",
                    model.BankCode ?? "",
                    model.AccountName ?? "",
                    model.Currency,
                    email);

                var result = await _mediator.Send(command, ct);

                if (result.Success)
                {
                    _notyf.Success("Withdrawal initiated successfully.");
                    return RedirectToAction("Dashboard");
                }

                ModelState.AddModelError(string.Empty, result.Message);
                await LoadBankListAsync(ct);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Withdrawal failed.");
                ModelState.AddModelError(string.Empty, $"Something went wrong: {ex.Message}");
                await LoadBankListAsync(ct);
                return View(model);
            }
        }

        private static List<SelectListItem>? _cachedBanks;
        private static DateTime _bankCacheExpiry;

        private async Task LoadBankListAsync(CancellationToken ct)
        {
            if (_cachedBanks != null && DateTime.UtcNow < _bankCacheExpiry)
            {
                ViewBag.Banks = new SelectList(_cachedBanks, "Value", "Text");
                return;
            }

            try
            {
                var banks = await _paystackService.ListBanksAsync("NGN", ct);
                _cachedBanks = banks
                    .OrderBy(b => b.Name)
                    .Select(b => new SelectListItem(b.Name, b.Code))
                    .ToList();

                // Cache for 12 hours so we don't hammer the Paystack API on every page load.
                _bankCacheExpiry = DateTime.UtcNow.AddHours(12);
                ViewBag.Banks = new SelectList(_cachedBanks, "Value", "Text");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load banks from Paystack; using fallback list.");
                ViewBag.Banks = new SelectList(GetFallbackBanks(), "Value", "Text");
            }
        }

        private static List<SelectListItem> GetFallbackBanks()
        {
            return new List<SelectListItem>
            {
                new SelectListItem("Access Bank", "044"),
                new SelectListItem("Guaranty Trust Bank (GTBank)", "058"),
                new SelectListItem("First Bank of Nigeria", "011"),
                new SelectListItem("United Bank for Africa (UBA)", "033"),
                new SelectListItem("Zenith Bank", "057"),
                new SelectListItem("Kuda Bank", "50211"),
                new SelectListItem("OPay Digital Services Limited (OPay)", "999992"),
                new SelectListItem("Moniepoint MFB", "50515")
            };
        }
    }
}