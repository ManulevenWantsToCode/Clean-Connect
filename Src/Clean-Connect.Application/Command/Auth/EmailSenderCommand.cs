using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace Clean_Connect.Application.Command.Auth
{
    public record EmailSenderCommand(string ToEmail, string Body, string Subject) : IRequest<Unit>;

    public class EmailSenderHandler : IRequestHandler<EmailSenderCommand, Unit>
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailSenderHandler> _logger;

        public EmailSenderHandler(IConfiguration configuration, ILogger<EmailSenderHandler> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<Unit> Handle(EmailSenderCommand request, CancellationToken cancellationToken)
        {
            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = int.Parse(_configuration["Email:Smtp:Port"]),
                Credentials = new NetworkCredential(
                    _configuration["Email:Smtp:Username"],
                    (_configuration["Email:Smtp:Password"] ?? "").Replace(" ", "").Trim()
                    
                    ),

                EnableSsl = true
            };

            var from = _configuration["Email:Smtp:From"];

            var mailMessage = new MailMessage()
            {
                From = new MailAddress(from, "Clean Connect"),
                Body = request.Body,
                Subject = request.Subject,
                IsBodyHtml = true
            };

            mailMessage.To.Add(request.ToEmail);

            try
            {
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail} with subject {Subject}", request.ToEmail, request.Subject);
            }

            return Unit.Value;
        }
    }
   
}
