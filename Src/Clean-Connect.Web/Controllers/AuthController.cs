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

namespace Clean_Connect.Web.Controllers
{
   
    public class AuthController : Controller
    {

        private readonly ILogger<AuthController> _logger;
        private readonly IMediator _mediator;
        private readonly INotyfService _notyf;
        private readonly UserManager<ApplicationUser> _user;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AuthController(ILogger<AuthController> logger, IMediator mediator, INotyfService notyf, UserManager<ApplicationUser> user, SignInManager<ApplicationUser> signInManager)
        {
            _logger = logger;
            _mediator = mediator;
            _notyf = notyf;
            _user = user;
            _signInManager = signInManager;
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

                //if (result.RequiresClientProfileCompletion)
                //{
                //    _logger.LogInformation("Redirecting to client profile creation.");
                //    return RedirectToAction(nameof(CreateClientProfile), "Clients");
                //}

                _notyf.Success("Login successful. Welcome back!");

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
        [HttpGet("Register-User")]
        public IActionResult Register()
        {
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
    

