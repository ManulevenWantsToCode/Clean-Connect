using Clean_Connect.Application.Command.Auth;
using Clean_Connect.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Command.ApplicationUserCommand
{
    public record RegisterUserCommand(string Email, string Password, string ConfirmPassword = "", string Role = "Client") : IRequest<Guid>;

    public class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
    {
        public RegisterUserValidator()
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
                .WithMessage("Password must be between 8-100 characters")
                .Matches("[A-Z]")
                .WithMessage("Password must contain at least one uppercase letter")
                .Matches("[a-z]")
                .WithMessage("Password must contain at least one lowercase letter")
                .Matches("[0-9]")
                .WithMessage("Password must contain at least one digit")
                .Matches("[^a-zA-Z0-9]")
                .WithMessage("Password must contain at least one special character");

            RuleFor(x => x.ConfirmPassword)
                .NotEmpty()
                .WithMessage("Please confirm your password")
                .Equal(x => x.Password)
                .WithMessage("Passwords do not match");

            RuleFor(x => x.Role)
                .NotEmpty()
                .WithMessage("Role is required")
                .Must(role => string.Equals(role, "Client", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, "Worker", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Role must be Client or Worker");
        }
    }

    public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Guid>
    {

        private readonly IConfiguration configuration;
        private readonly UserManager<ApplicationUser> user;
        private readonly ILogger<RegisterUserCommandHandler> logger;
        private readonly IMediator mediator;

        public RegisterUserCommandHandler(IConfiguration _configuration, ILogger<RegisterUserCommandHandler> _logger, IMediator _mediator, UserManager<ApplicationUser> _user)
        {
            logger = _logger;
            mediator = _mediator;
            user = _user;
            configuration = _configuration;
        }


        public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
        {

            var email = request.Email.Trim().ToLower();
            var checkExistingUser = await user.FindByEmailAsync(email);
            if (checkExistingUser != null)
            {
                logger.LogWarning("User with email {Email} already exists", email);

                throw new ValidationException("User with this email already exists");

            }

            var newUser = ApplicationUser.Create(email);

            var result = await user.CreateAsync(newUser, request.Password);



            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));

                logger.LogError("Failed to create user with email {Email}. Errors: {Errors}", email, errors);
                throw new ValidationException($"Failed to create user: {errors}");
            }

            var role = string.Equals(request.Role, "Worker", StringComparison.OrdinalIgnoreCase)
                ? "Worker"
                : "Client";
            var roleResult = await user.AddToRoleAsync(newUser, role);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));

                logger.LogError("Failed to assign role {Role} to user with email {Email}. Errors: {Errors}", role, email, errors);
                throw new ValidationException($"Failed to assign role: {errors}");
            }

            var token = await user.GenerateEmailConfirmationTokenAsync(newUser);

            var baseUrl = configuration["App:ClientUrl"];

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new Exception("App:ClientUrl is missing in configuration");

            var confirmationLink = $"{baseUrl.TrimEnd('/')}/confirm-email?userId={newUser.Id}&token={Uri.EscapeDataString(token)}";

            var emailMessage = new EmailSenderCommand(
                ToEmail: newUser.Email,
                Subject: "Confirm your email",
                Body: $@"
                        <html>
                          <body>
                            <p>Please confirm your email by clicking the button below:</p>

                            <a href='{confirmationLink}'
                               style='display:inline-block;padding:12px 20px;background:#28a745;
                               color:#ffffff;text-decoration:none;border-radius:6px;font-weight:bold;'>
                               Confirm Email
                            </a>

                           
                          </body>
                        </html>"
                );

            await mediator.Send(emailMessage, cancellationToken);


            logger.LogInformation("User with email {Email} created successfully with role {Role}", request.Email, role);
            return newUser.Id;

        }

    }
}
