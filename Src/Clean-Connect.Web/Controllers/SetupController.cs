using AspNetCoreHero.ToastNotification.Abstractions;
using Clean_Connect.Application.DTO;
using Clean_Connect.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Clean_Connect.Web.Controllers
{
    public class SetupController : Controller
    {
        private readonly UserManager<ApplicationUser> _user;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SetupController> _logger;
        private readonly INotyfService _notyf;

        public SetupController(
            UserManager<ApplicationUser> user,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ILogger<SetupController> logger,
            INotyfService notyf)
        {
            _user = user;
            _signInManager = signInManager;
            _configuration = configuration;
            _logger = logger;
            _notyf = notyf;
        }

        [HttpGet("setup-admin")]
        [AllowAnonymous]
        public async Task<IActionResult> SetupAdmin()
        {
            if (await SetupIsLockedAsync())
                return NotFound();

            return View(new SetupAdminDto());
        }

        [HttpPost("setup-admin")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetupAdmin(SetupAdminDto model)
        {
            if (await SetupIsLockedAsync())
                return NotFound();

            if (!IsSetupKeyValid(model.SetupKey))
            {
                _logger.LogWarning("Admin setup attempt blocked: invalid setup key (email {Email}).", model.Email);
                _notyf.Error("Invalid admin setup key.");
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                _notyf.Error("Please correct the highlighted errors.");
                return View(model);
            }

            var existing = await _user.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                _logger.LogWarning("Admin setup attempt failed: email {Email} already in use.", model.Email);
                _notyf.Error("An account with that email already exists.");
                return View(model);
            }

            var admin = ApplicationUser.Create(model.Email);
            admin.EmailConfirmed = true;

            var createResult = await _user.CreateAsync(admin, model.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Admin setup failed to create user {Email}: {Errors}", model.Email, errors);
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                _notyf.Error("Unable to create the administrator account.");
                return View(model);
            }

            var roleResult = await _user.AddToRoleAsync(admin, "Admin");
            if (!roleResult.Succeeded)
            {
                var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError("Admin setup failed to assign role for {Email}: {Errors}", model.Email, errors);
                await _user.DeleteAsync(admin);
                _notyf.Error("Unable to create the administrator account.");
                return View(model);
            }

            _logger.LogInformation("Initial administrator account created for {Email}.", model.Email);
            _notyf.Success("Administrator account created. Welcome!");

            await _signInManager.SignInAsync(admin, isPersistent: true);
            return RedirectToAction(nameof(AdminController.Index), "Admin");
        }

        private async Task<bool> SetupIsLockedAsync()
        {
            if (string.IsNullOrWhiteSpace(_configuration["Admin:SetupKey"]))
            {
                _logger.LogWarning("/setup-admin blocked: Admin:SetupKey is not configured in appsettings.");
                return true;
            }

            var admins = await _user.GetUsersInRoleAsync("Admin");
            return admins.Count > 0;
        }

        private bool IsSetupKeyValid(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var expected = _configuration["Admin:SetupKey"] ?? string.Empty;
            var a = Encoding.UTF8.GetBytes(input);
            var b = Encoding.UTF8.GetBytes(expected);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}