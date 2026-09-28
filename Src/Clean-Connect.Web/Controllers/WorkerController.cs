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
                    worker.DateOfBirth,
                    worker.HourlyRate);

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

        [HttpGet("Payout-Details")]
        public async Task<IActionResult> PayoutDetails(CancellationToken ct)
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

                var detail = await _mediator.Send(new GetWorkerBankDetailQuery(worker.Id), ct);
                var model = new PayoutDetailsViewModel
                {
                    HasDetails = detail.HasDetails,
                    BankCode = detail.BankCode,
                    BankName = detail.BankName,
                    AccountNumber = string.Empty,
                    AccountName = detail.AccountName,
                    Currency = detail.Currency,
                    MaskedAccountNumber = detail.HasDetails ? detail.MaskedAccountNumber : string.Empty
                };
                await LoadBankListAsync(ct);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load payout details page.");
                _notyf.Error("Unable to load payout details.");
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost("Payout-Details")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayoutDetails(PayoutDetailsViewModel model, CancellationToken ct)
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

                var resolvedName = await ResolveAccountNameAsync(model.BankCode, model.AccountNumber, model.AccountName, ct);
                if (!string.IsNullOrWhiteSpace(resolvedName))
                    model.AccountName = resolvedName;

                if (!ModelState.IsValid)
                {
                    await LoadBankListAsync(ct);

                    var modelErrors = ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Distinct()
                        .ToList();

                    _logger.LogWarning("Payout details save blocked. Invalid fields: {Fields}", string.Join(" | ", ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .Select(x => $"{x.Key}: {string.Join(", ", x.Value!.Errors.Select(e => e.ErrorMessage))}")));

                    _notyf.Error(modelErrors.Count > 0
                        ? string.Join(" ", modelErrors)
                        : "Please correct the highlighted errors.");

                    return View(model);
                }

                var command = new SaveWorkerBankDetailCommand(
                    worker.Id,
                    model.BankCode,
                    model.BankName,
                    model.AccountNumber,
                    model.AccountName,
                    model.Currency,
                    email);

                var result = await _mediator.Send(command, ct);

                if (result)
                {
                    _notyf.Success("Payout bank details saved. Any pending payout will be sent automatically.");
                    return RedirectToAction("Dashboard");
                }

                await LoadBankListAsync(ct);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save payout details.");
                ModelState.AddModelError(string.Empty, ex.Message);
                _notyf.Error(ex.Message);
                await LoadBankListAsync(ct);
                return View(model);
            }
        }

        [HttpPost("Resolve-Account")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveAccount(string bankCode, string accountNumber, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(bankCode) || string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Trim().Length != 10)
            {
                return Json(new { ok = false, message = "Enter a valid bank and a 10-digit account number." });
            }

            var resolved = await _paystackService.ResolveAccountAsync(accountNumber.Trim(), bankCode.Trim(), ct);
            if (resolved?.Status == true && !string.IsNullOrWhiteSpace(resolved.Data?.AccountName))
            {
                return Json(new { ok = true, accountName = resolved.Data.AccountName });
            }

            return Json(new { ok = false, message = "Could not verify this account number. Check the bank and account number and try again." });
        }

        private async Task<string> ResolveAccountNameAsync(string bankCode, string accountNumber, string currentName, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(currentName))
                return currentName;

            if (string.IsNullOrWhiteSpace(bankCode) || string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Trim().Length != 10)
                return string.Empty;

            var resolved = await _paystackService.ResolveAccountAsync(accountNumber.Trim(), bankCode.Trim(), ct);
            return resolved?.Status == true ? (resolved.Data?.AccountName ?? string.Empty) : string.Empty;
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