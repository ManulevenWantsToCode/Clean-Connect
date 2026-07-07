using Clean_Connect.Application.Command.Auth;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Clean_Connect.Application.Command.ApplicationUserCommand
{
    public record LoginCommand(string Email, string Password, bool RememberMe) : IRequest<LoginResponse>;

    public class LoginCommandValidator : AbstractValidator<LoginCommand>
    {
        public LoginCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email is required")
                .EmailAddress()
                .WithMessage("Email is invalid")
                .Length(10, 100)
                .WithMessage("Email must be between 10-100 characters");
            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("Password is required")
                .Length(8, 100)
                .WithMessage("Password must be between 8-100 characters");
        }
    }

    public class LoginCommandHandler(UserManager<ApplicationUser> user, IMediator _mediator, SignInManager<ApplicationUser> signInManager, ILogger<RegisterUserCommandHandler> logger, IConfiguration configuration) : IRequestHandler<LoginCommand, LoginResponse>
    {
        public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            // Find the user
            var appUser = await user.FindByEmailAsync(request.Email);

            if (appUser == null)
            {
                logger.LogWarning("Login failed for email: {Email} (user not found)", request.Email);

                return new LoginResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = "Invalid email or password."
                };
            }

            // Check if account is already locked
            if (await user.IsLockedOutAsync(appUser))
            {
                logger.LogWarning("Locked out login attempt for {Email}", request.Email);

                return new LoginResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = "Your account is locked. Please try again later.",
                    UserId = appUser.Id
                };
            }

            // Verify password
            var passwordValid = await user.CheckPasswordAsync(appUser, request.Password);

            if (!passwordValid)
            {
                // Increment failed login count
                await user.AccessFailedAsync(appUser);

                // Check if this failed attempt locked the account
                if (await user.IsLockedOutAsync(appUser))
                {
                    logger.LogWarning("User {Email} has been locked out.", request.Email);

                    return new LoginResponse
                    {
                        IsSuccessful = false,
                        ErrorMessage = "Your account has been locked due to multiple failed login attempts.",
                        UserId = appUser.Id
                    };
                }

                logger.LogWarning("Invalid password for {Email}", request.Email);

                return new LoginResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = "Invalid email or password.",
                    UserId = appUser.Id
                };
            }

            // Password is correct, reset failed attempts
            await user.ResetAccessFailedCountAsync(appUser);

            // Now check email confirmation
            if (!appUser.EmailConfirmed)
            {
                logger.LogInformation("Login blocked for {Email}: email not confirmed.", request.Email);

                return new LoginResponse
                {
                    IsSuccessful = false,
                    EmailNotConfirmed = true,
                    ErrorMessage = "Please confirm your email before logging in.",
                    UserId = appUser.Id
                };
            }

            // Sign in (creates authentication cookie)
            await signInManager.SignInAsync(appUser, request.RememberMe);

            // Get roles
            var roles = await user.GetRolesAsync(appUser);

            if (roles.Contains("Worker") && !appUser.IsWorkerProfileCompleted)
            {
                return new LoginResponse
                {
                    UserId = appUser.Id,
                    Email = appUser.Email!,
                    Roles = roles.ToArray(),
                    IsSuccessful = true,
                    RequiresWorkerProfileCompletion = true
                };
            }

            if (roles.Contains("Client") && !appUser.IsClientProfileCompleted)
            {
                return new LoginResponse
                {
                    UserId = appUser.Id,
                    Email = appUser.Email!,
                    Roles = roles.ToArray(),
                    IsSuccessful = true,
                    RequiresClientProfileCompletion = true
                };
            }

            // Generate JWT
            var token = await _mediator.Send(
                new JwtTokenCommand(appUser),
                cancellationToken);

            logger.LogInformation("User logged in successfully: {Email}", request.Email);

            return new LoginResponse
            {
                UserId = appUser.Id,
                Email = appUser.Email!,
                Roles = roles.ToArray(),
                Token = token,
                IsSuccessful = true
            };
        }
    }
}
