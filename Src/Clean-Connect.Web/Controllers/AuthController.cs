using AspNetCoreGeneratedDocument;
using AspNetCoreHero.ToastNotification.Abstractions;
using Clean_Connect.Application.Command.ApplicationUserCommand;
using Clean_Connect.Application.Command.Auth;
using Clean_Connect.Application.DTO;
using Clean_Connect.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Clean_Connect.Web.Controllers
{
   
    public class AuthController : Controller
    {

        private readonly ILogger<AuthController> _logger;
        private readonly IMediator _mediator;
        private readonly INotyfService _notyf;
        private readonly UserManager<ApplicationUser> _user;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;

        public AuthController(ILogger<AuthController> logger, IMediator mediator, INotyfService notyf, UserManager<ApplicationUser> user, SignInManager<ApplicationUser> signInManager, IConfiguration configuration)
        {
            _logger = logger;
            _mediator = mediator;
            _notyf = notyf;
            _user = user;
            _signInManager = signInManager;
            _configuration = configuration;
        }

        [HttpGet("Login")]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View(new LoginDto());
        }

        [HttpPost("Login")]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginDto model, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Login validation failed for {Email}", model.Email);
                _notyf.Error("Please correct the highlighted errors.");
                return View(model);
            }

            try
            {
                _logger.LogInformation("Login attempt for {Email}", model.Email);

                var command = new LoginCommand(
                    model.Email,
                    model.Password,
                    model.RememberMe);

                var result = await _mediator.Send(command, cancellationToken);

                // Email not confirmed
                if (result.EmailNotConfirmed == true)
                {
                    _logger.LogInformation(
                        "Unconfirmed email login attempt for {Email}",
                        model.Email);

                    _notyf.Warning(
                        "Your email address has not been verified.");

                    return RedirectToAction(
                        nameof(EmailNotConfirmed),
                        new { email = model.Email });
                }


                // Invalid login
                if (result == null || !result.IsSuccessful)
                {
                    _logger.LogWarning(
                        "Failed login attempt for {Email}. Reason: {Error}",
                        model.Email,
                        result?.ErrorMessage);

                    _notyf.Error(
                        result?.ErrorMessage ??
                        "Invalid email or password.");

                    ModelState.AddModelError(
                        string.Empty,
                        result?.ErrorMessage ??
                        "Invalid email or password.");

                    return View(model);
                }


                _logger.LogInformation(
                    "User {Email} logged in successfully",
                    model.Email);

                

                if (result.RequiresWorkerProfileCompletion)
                {
                    _notyf.Information("Please Complete your profile");
                    return RedirectToAction(nameof(WorkerController.CreateWorkerProfile), "Worker");
                }

                if (result.RequiresClientProfileCompletion)
                {
                    _logger.LogInformation("Redirecting to client profile creation.");
                    return RedirectToAction(nameof(ClientController.CreateClientProfile), "Client");
                }

                _notyf.Success("Login successful. Welcome back!");

                if (result.Roles.Contains("Admin"))
                    return RedirectToAction(nameof(AdminController.Index), "Admin");

                if (result.Roles.Contains("Worker"))
                    return RedirectToAction(nameof(WorkerController.Dashboard), "Worker");

                if (result.Roles.Contains("Client"))
                    return RedirectToAction(nameof(ClientController.Dashboard), "Client");

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while logging in {Email}",
                    model.Email);

                _notyf.Error(
                    "An unexpected error occurred. Please try again.");

                return View(model);
            }
        }
        [HttpGet("Google-Login")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleLogin(string? returnUrl = null, string? role = null)
        {
            var googleConfig = _configuration.GetSection("Authentication:Google");
            if (string.IsNullOrWhiteSpace(googleConfig["ClientId"]) || string.IsNullOrWhiteSpace(googleConfig["ClientSecret"]))
            {
                _notyf.Error("Google sign-in is not configured. Please sign in with your email and password.");
                return RedirectToAction(nameof(Login));
            }

            if (string.Equals(role, "Worker", StringComparison.OrdinalIgnoreCase))
            {
                Response.Cookies.Append("GoogleSignupRole", "Worker", new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromMinutes(5)
                });
            }
            else if (string.Equals(role, "Client", StringComparison.OrdinalIgnoreCase))
            {
                Response.Cookies.Append("GoogleSignupRole", "Client", new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromMinutes(5)
                });
            }

            var callbackUrl = Url.Action(nameof(GoogleCallback), "Auth", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(
                "Google", callbackUrl);
            return Challenge(properties, "Google");
        }

        [HttpGet("Google-Callback")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (!string.IsNullOrWhiteSpace(remoteError))
            {
                _logger.LogWarning("Google OAuth failed with error: {RemoteError}", remoteError);
                _notyf.Error("There was a problem signing in with Google. Please try again.");
                return RedirectToAction(nameof(Login));
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogWarning("Google OAuth callback received no external login info.");
                _notyf.Error("Unable to sign in with Google. Please try again.");
                return RedirectToAction(nameof(Login));
            }

            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                Response.Cookies.Delete("GoogleSignupRole");
                _logger.LogInformation("User signed in via {Provider}.", info.LoginProvider);
                _notyf.Success("Login successful. Welcome back!");
                return RedirectToDashboard(returnUrl);
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("Google OAuth did not provide an email address.");
                _notyf.Error("Google did not provide an email address. Please sign in with your email and password instead.");
                return RedirectToAction(nameof(Login));
            }

            email = email.Trim().ToLowerInvariant();
            var user = await _user.FindByEmailAsync(email);

            if (user == null)
            {
                user = ApplicationUser.Create(email);
                user.EmailConfirmed = true;

                var createResult = await _user.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create user from Google login for {Email}. Errors: {Errors}", email, errors);
                    _notyf.Error("Unable to create your account from the Google login.");
                    return RedirectToAction(nameof(Login));
                }

                var role = "Client";
                if (Request.Cookies.TryGetValue("GoogleSignupRole", out var cookieRole) &&
                    string.Equals(cookieRole, "Worker", StringComparison.OrdinalIgnoreCase))
                {
                    role = "Worker";
                }
                Response.Cookies.Delete("GoogleSignupRole");

                var roleResult = await _user.AddToRoleAsync(user, role);
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Failed to assign {Role} role after Google login for {Email}.", role, email);
                }

                _logger.LogInformation("Created user {Email} with role {Role} via Google OAuth.", email, role);
            }
            else
            {
                Response.Cookies.Delete("GoogleSignupRole");

                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    await _user.UpdateAsync(user);
                    _logger.LogInformation("Confirmed email for existing user {Email} via Google OAuth.", email);
                }
            }

            var loginInfo = new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.LoginProvider);
            var linkResult = await _user.AddLoginAsync(user, loginInfo);
            if (!linkResult.Succeeded)
            {
                var errors = string.Join(", ", linkResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to link Google login to {Email}. Errors: {Errors}", email, errors);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = null;

            if (await _user.IsInRoleAsync(user, "Worker"))
            {
                if (!user.IsWorkerProfileCompleted)
                {
                    _notyf.Information("Please complete your profile.");
                    return RedirectToAction(nameof(WorkerController.CreateWorkerProfile), "Worker");
                }
                return RedirectToAction(nameof(WorkerController.Dashboard), "Worker");
            }

            if (!user.IsClientProfileCompleted)
            {
                _notyf.Information("Please complete your profile.");
                return RedirectToAction(nameof(ClientController.CreateClientProfile), "Client");
            }

            return RedirectToAction(nameof(ClientController.Dashboard), "Client");
        }

        private IActionResult RedirectToDashboard(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (User.IsInRole("Admin"))
                return RedirectToAction(nameof(AdminController.Index), "Admin");
            if (User.IsInRole("Worker"))
                return RedirectToAction(nameof(WorkerController.Dashboard), "Worker");
            if (User.IsInRole("Client"))
                return RedirectToAction(nameof(ClientController.Dashboard), "Client");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet("Register-User")]
        public IActionResult Register([FromQuery(Name = "refCode")] string? refCode)
        {
            if (!string.IsNullOrWhiteSpace(refCode))
            {
                Response.Cookies.Append(
                    "ReferralCode",
                    refCode.Trim(),
                    new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Lax,
                        MaxAge = TimeSpan.FromDays(30)
                    });

                ViewData["ReferralCode"] = refCode.Trim();
            }

            return View();
        }
        [HttpPost("Register-User")]
        public async Task<IActionResult> Register([FromForm] RegisterUserCommand request, CancellationToken cancellationToken)
        {

            if (!ModelState.IsValid)
            {
                _notyf.Error("Please correct the errors in the form.");
                return View(request);
            }

            try
            {
                var result = await _mediator.Send(request, cancellationToken);
                // Fix: 'result' is a Guid, not an object with 'Success' property.
                // Assume registration is successful if Guid is not empty.
                if (result != Guid.Empty)
                {
                    _notyf.Success("User registered successfully!");
                    return RedirectToAction(nameof(PendingConfirmation));
                }

                _notyf.Error("Failed to register user.");
                return View("Error", null);
            }
            catch (FluentValidation.ValidationException vex)
            {
                _logger.LogWarning(vex, "Registration validation failed.");
                var failures = vex.Errors?.ToList() ?? new List<FluentValidation.Results.ValidationFailure>();

                if (failures.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, vex.Message);
                    failures.Add(new FluentValidation.Results.ValidationFailure(string.Empty, vex.Message));
                }
                else
                {
                    foreach (var failure in failures)
                        ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
                }

                var isDuplicateEmail = failures.Any(f =>
                    f.ErrorMessage.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0);

                _notyf.Error(isDuplicateEmail
                    ? "User with this email already exists."
                    : "Please fix the errors below and try again.");
                return View(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed.");
                _notyf.Error("Something went wrong while creating your account. Please try again.");
                return View(request);
            }
        }

        [HttpGet("PendingConfirmation")]
        public IActionResult PendingConfirmation(string email)
        {
            return View(model: email);
        }

        [HttpPost("Resend-Email")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendConfirmationEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("Resend confirmation email attempted with empty email.");
                _notyf.Warning("Invalid email address.");

                return RedirectToAction(nameof(EmailNotConfirmed));
            }

            try
            {
                var result = await _mediator.Send(new ResendEmailConfirmationCommand(email));

                if (result)
                {
                    _logger.LogInformation(
                        "Verification email resent successfully to {Email}",
                        email);

                    _notyf.Success(
                        "A new verification email has been sent. Please check your inbox.");

                    return RedirectToAction(nameof(EmailNotConfirmed), new { email });
                }

                _logger.LogWarning(
                    "Resend confirmation email failed for {Email}",
                    email);

                _notyf.Warning(
                    "Unable to resend verification email. The email may already be confirmed or you may need to wait before trying again.");

                return RedirectToAction(nameof(EmailNotConfirmed), new { email });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while resending confirmation email for {Email}",
                    email);

                _notyf.Error(
                    "Something went wrong while sending the verification email.");

                return View("Error");
            }
        }

        [HttpGet("Email-not-confirmed")]
        public IActionResult EmailNotConfirmed(string email)
        {
            _logger.LogInformation(
                "EmailNotConfirmed page accessed for {Email}",
                email); 



            return View("PendingConfirmation", model: email);
        }

        [HttpGet("Confirm-Email")]

        public async Task<IActionResult> ConfirmEmail(Guid userId, string token, CancellationToken cancellationToken)
        {
            var command = new ConfirmEmailCommand(userId.ToString(), token);

            var result = await _mediator.Send(command, cancellationToken);
            if (result)
            {
                _notyf.Success("Email confirmed successfully!");
                return View("ConfirmEmail");
            }
            _notyf.Error("Failed to confirm email.");
            return View("Error", null);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
           return View(new ForgotPasswordDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Forgot password validation failed for {Email}", model.Email);
                _notyf.Error("Please correct the highlighted errors.");
                return View(model);
            }

            try
            {
                var command = new ForgotPasswordCommand(model.Email);
                _logger.LogInformation("Processing forgot password request for {Email}", model.Email);

                await _mediator.Send(command);

                return RedirectToAction(nameof(ResetLinkSent));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing forgot password request for {Email}", model.Email);
                _notyf.Error("An error occurred while processing your request.");
                
            }
            return View(model);



        }

        [HttpGet("Reset-Password")]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Reset password attempted with missing email or token.");
                _notyf.Error("Invalid password reset link.");
                return View("Error");
            }
            var model = new ResetPasswordDto
            {
                Email = email,
                Token = token
            };
            return View(model);
        }


        [HttpPost("Reset-Password")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Reset password validation failed for {Email}", model.Email);
                _notyf.Error("Please correct the highlighted errors.");
                return View(model);
            }
            try
            {
                var command = new ResetPasswordCommand(model.Email, model.Token, model.Password);
                _logger.LogInformation("Processing reset password request for {Email}", model.Email);
                var result = await _mediator.Send(command);
                if (result)
                {
                    _notyf.Success("Password has been reset successfully!");
                    return RedirectToAction(nameof(PasswordResetSuccess));
                }
                else
                {
                    _logger.LogWarning("Failed to reset password for {Email}. Invalid token or email.", model.Email);
                    _notyf.Error("Failed to reset password. The link may be invalid or expired.");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while resetting password for {Email}", model.Email);
                _notyf.Error("An error occurred while processing your request.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult PasswordResetSuccess()
        {
            return View();
        }

        public IActionResult ResetLinkSent()
        {
            return View();
        }


        [HttpGet("Profile")]
        public IActionResult Profile()
        {
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}
    

