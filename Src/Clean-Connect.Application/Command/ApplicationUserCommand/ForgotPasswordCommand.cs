using Clean_Connect.Application.Command.Auth;
using Clean_Connect.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Command.ApplicationUserCommand
{
    public record ForgotPasswordCommand(string Email) : IRequest<bool>;

    public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
    {

        public ForgotPasswordCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email is required")
                .EmailAddress()
                .WithMessage("Email is invalid")
                .Length(10, 100)
                .WithMessage("Email must be between 10-100 characters");
        }

        public class ForgotPasswordCommandHandler(UserManager<ApplicationUser> userManager, IMediator mediator, IConfiguration configuration) : IRequestHandler<ForgotPasswordCommand, bool>
        {
            public async Task<bool> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
            {
                var user = await userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return false;
                }
                var token = await userManager.GeneratePasswordResetTokenAsync(user);

                var encodedToken = Uri.EscapeDataString(token);

                var baseUrl = configuration["App:ClientUrl"];

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new InvalidOperationException(
                        "App:ClientUrl is missing from configuration.");
                }

                // Use the server-side MVC route that AuthController exposes.
                // AuthController has [HttpGet("Reset-Password")] so the endpoint is "/Reset-Password".
                var resetLink = $"{baseUrl.TrimEnd('/')}/Reset-Password?email={Uri.EscapeDataString(user.Email)}&token={encodedToken}";
                var emailBody = $"<p>You requested a password reset. Click the link below to reset your password:</p><p><a href='{resetLink}'>Reset Password</a></p>";
                var emailSubject = "Password Reset Request";
                await mediator.Send(new EmailSenderCommand(request.Email, emailBody, emailSubject));
                return true;
            }




        }

    }
}
