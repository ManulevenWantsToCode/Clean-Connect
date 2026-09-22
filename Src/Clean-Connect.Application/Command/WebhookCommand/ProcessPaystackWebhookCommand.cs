using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Helper;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Clean_Connect.Application.Command.WebhookCommand
{
    public record ProcessPaystackWebhookCommand(string Payload, string Signature) : IRequest<Unit>;
    public class ProcessPaystackWebhookCommandHandler : IRequestHandler<ProcessPaystackWebhookCommand, Unit>
    {

        private readonly ILogger<ProcessPaystackWebhookCommandHandler> logger;
        private readonly IPaystackService paystackService;
        private readonly EscrowService escrowService;
        private readonly INotificationService notificationService;

        private readonly IUnitOfWork unitOfWork;
        private readonly IConfiguration configuration;
        public ProcessPaystackWebhookCommandHandler(ILogger<ProcessPaystackWebhookCommandHandler> _logger, IPaystackService _paystackService, EscrowService escrowService, INotificationService notificationService, IUnitOfWork _unitOfWork, IConfiguration _configuration)
        {
            logger = _logger;
            paystackService = _paystackService;
            this.escrowService = escrowService;
            this.notificationService = notificationService;

            unitOfWork = _unitOfWork;
            configuration = _configuration;
        }
        public async Task<Unit> Handle(ProcessPaystackWebhookCommand request, CancellationToken cancellationToken)
        {
            // 1. Verify signature
            var secret = configuration["Paystack:SecretKey"];
            var computedHash = ComputeHash(request.Payload, secret);

            if (computedHash != request.Signature)
                throw new UnauthorizedAccessException("Invalid signature");

            var payload = JsonSerializer.Deserialize<PaystackWebhookDto>(request.Payload);
            logger.LogInformation("Received Paystack webhook event: {Event}", payload.Event);

            if (payload.Event != "charge.success")
                
            return Unit.Value;

            var reference = payload.Data.Reference;
            logger.LogInformation("Processing payment with reference: {Reference}", reference);

            // 2. get payment
            var payment =  await unitOfWork.Payments.GetByReferenceAsync(reference, cancellationToken);

            if (payment == null)
            {
                logger.LogWarning("Payment with reference {Reference} not found", reference);
                throw new Exception("Payment not found");
            }

            // 🔒 Prevent double processing (VERY IMPORTANT)
            if (payment.Status == PaymentStatus.Successful)
            {
                logger.LogInformation("Payment with reference {Reference} has already been processed", reference);
                return Unit.Value;
            }

            var verification = await paystackService.VerifyTransaction(reference);
            if (!verification.Status || verification.Data.Status != "success")
            {
                payment.MarkAsFailed(verification.Data?.GatewayResponse?? verification.Message,verification.Data?.Id?.ToString());
                await unitOfWork.Payments.UpdatePayment(payment, cancellationToken);
                return Unit.Value;
            }

            // 4. Validate amount
            var amountPaid = verification.Data.Amount / 100m;
            logger.LogInformation("Amount paid: {AmountPaid}, Expected amount: {ExpectedAmount}", amountPaid, payment.Amount);

            if (amountPaid != payment.Amount) 
            {
                logger.LogError("Amount mismatch for payment reference {Reference}. Amount paid: {AmountPaid}, Expected amount: {ExpectedAmount}", reference, amountPaid, payment.Amount);
                throw new Exception("Amount mismatch");
            }
                

            // 5. Get booking
            var booking = await unitOfWork.Bookings.GetBookingById(payment.BookingId, cancellationToken);
            if (booking == null)
            {
                logger.LogError("Booking not found for payment reference {Reference}", reference);
                throw new Exception("Booking not found");
            }

            // 🔒 Extra validation (VERY IMPORTANT)
            if (booking.Amount != payment.Amount)
            {
                logger.LogError("Booking amount mismatch for payment reference {Reference}. Booking amount: {BookingAmount}, Payment amount: {PaymentAmount}", reference, booking.Amount, payment.Amount);
                throw new Exception("Booking amount mismatch");
            }

            var transactionId = verification.Data.Id?.ToString() ?? reference;
            payment.MarkAsPaid(verification.Data.Authorization.AuthorizationCode, transactionId);
            logger.LogInformation("Payment with reference {Reference} marked as paid", reference);

            booking.MarkAsPaid();
            logger.LogInformation("Booking with reference {Reference} marked as paid", reference);

            await escrowService.HoldPaymentInEscrowAsync(booking, payment, cancellationToken);

            await unitOfWork.Payments.UpdatePayment(payment, cancellationToken);
            await unitOfWork.Bookings.UpdateBooking(booking, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notificationService.PaymentConfirmedAsync(booking, cancellationToken);

            return Unit.Value;
        }
        private string ComputeHash(string payload, string secret)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();

        }
    }


}
