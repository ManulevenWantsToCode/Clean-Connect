using AspNetCoreHero.ToastNotification.Abstractions;
using Clean_Connect.Application.Command.ClientCommands;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Query.ClientsQuery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Web.Controllers
{
    [Authorize]
    public class ClientController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _repo;
        private readonly INotyfService _notyf;
        private readonly ILogger<ClientController> _logger;

        public ClientController(IMediator mediator, IUnitOfWork repo, INotyfService notyf, ILogger<ClientController> logger)
        {
            _mediator = mediator;
            _repo = repo;
            _notyf = notyf;
            _logger = logger;
        }

        [HttpGet("Create-Client-Profile")]
        public IActionResult CreateClientProfile()
        {
            if (Request.Cookies.TryGetValue("ReferralCode", out var refCode) &&
                !string.IsNullOrWhiteSpace(refCode))
            {
                ViewData["ReferralCode"] = refCode.Trim();
                Response.Cookies.Delete("ReferralCode");
            }

            return View();
        }

        [HttpPost("Create-Client-Profile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClientProfile(CreateClientCommand command, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _notyf.Error("Please correct the highlighted errors.");
                return View("CreateClientProfile", command);
            }

            try
            {
                var result = await _mediator.Send(command, ct);

                if (result)
                {
                    _notyf.Success("Client profile created successfully.");
                    return RedirectToAction("Dashboard");
                }

                _notyf.Error("Unable to create client profile.");
                return View("CreateClientProfile", command);
            }
            catch (FluentValidation.ValidationException vex)
            {
                _logger.LogWarning(vex, "Client profile creation validation failed.");
                _notyf.Error(vex.Message);
                return View("CreateClientProfile", command);
            }
            catch (UnauthorizedAccessException uex)
            {
                _logger.LogWarning(uex, "Client profile creation unauthorized.");
                _notyf.Error(uex.Message);
                return RedirectToAction("Login", "Auth");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client profile creation failed.");
                _notyf.Error($"Something went wrong: {ex.Message}");
                return View("CreateClientProfile", command);
            }
        }

        [HttpGet("Client-Dashboard")]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            try
            {
                var email = User.Identity?.Name;
                if (!string.IsNullOrEmpty(email))
                {
                    var existingClient = await _repo.Clients.GetByEmail(email, ct);
                    if (existingClient == null)
                    {
                        _notyf.Information("Please complete your profile first.");
                        return RedirectToAction(nameof(CreateClientProfile));
                    }
                }

                var model = await _mediator.Send(new GetClientDashboardQuery(), ct);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load client dashboard");
                _notyf.Error("Unable to load dashboard data.");
                return View(new ClientDashboardDto
                {
                    ClientName = User.Identity?.Name ?? "Client"
                });
            }
        }

        [HttpGet("Update-Client-Details")]
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

                var client = await _repo.Clients.GetByEmail(email, ct);
                if (client == null)
                {
                    _notyf.Error("Client not found.");
                    return RedirectToAction("Dashboard");
                }

                var command = new UpdateClientCommand(
                    client.Id,
                    client.FullName.FirstName,
                    client.FullName.LastName,
                    client.PhoneNumber.Value,
                    client.Gender.ToString(),
                    client.State,
                    client.Address.Value,
                    client.Location?.Latitude ?? 8.5,
                    client.Location?.Longitude ?? 6.5,
                    client.DateOfBirth);

                return View(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load client details for update.");
                _notyf.Error("Unable to load client details.");
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost("Update-Client-Details")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDetails(UpdateClientCommand command, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                _notyf.Error("Please correct the highlighted errors.");
                return View(command);
            }

            try
            {
                var result = await _mediator.Send(command, ct);

                if (result)
                {
                    _notyf.Success("Client details updated successfully.");
                    return RedirectToAction("Dashboard");
                }

                _notyf.Error("Unable to update client details.");
                return View(command);
            }
            catch (FluentValidation.ValidationException vex)
            {
                _logger.LogWarning(vex, "Client update validation failed.");
                _notyf.Error(vex.Message);
                return View(command);
            }
            catch (KeyNotFoundException)
            {
                _notyf.Error("Client not found.");
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client update failed.");
                _notyf.Error($"Something went wrong: {ex.Message}");
                return View(command);
            }
        }
    }
}
