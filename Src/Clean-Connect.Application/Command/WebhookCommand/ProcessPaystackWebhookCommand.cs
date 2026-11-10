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
        private static readonly JsonSerializerOptions WebhookJsonOptions = new(JsonSerializerDefaults.Web);

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
            if (string.IsNullOrWhiteSpace(secret) || !IsValidSignature(request.Payload, request.Signature, secret))
                throw new UnauthorizedAccessException("Invalid signature");

            var payload = JsonSerializer.Deserialize<PaystackWebhookDto>(request.Payload, WebhookJsonOptions)
                ?? throw new InvalidOperationException("Paystack webhook payload was empty or invalid.");
            logger.LogInformation("Received Paystack webhook event: {Event}", payload.Event);

            if (payload.Event is "transfer.success" or "transfer.failed" or "transfer.reversed")
            {
                await ProcessTransferWebhookAsync(payload, cancellationToken);
                return Unit.Value;
            }

            if (payload.Event != "charge.success")
                return Unit.Value;

            if (payload.Data == null || string.IsNullOrWhiteSpace(payload.Data.Reference))
                throw new InvalidOperationException("Paystack charge webhook did not include a payment reference.");

            var reference = payload.Data.Reference;
            logger.LogInformation("Processing a verified Paystack payment webhook.");

            // 2. get payment
            var payment =  await unitOfWork.Payments.GetByReferenceAsync(reference, cancellationToken);

            if (payment == null)
            {
                logger.LogWarning("Verified Paystack webhook did not match a payment record.");
                throw new InvalidOperationException("Payment not found.");
            }

            // 🔒 Prevent double processing (VERY IMPORTANT)
            if (payment.Status == PaymentStatus.Successful)
            {
                logger.LogInformation("Paystack webhook was already processed for payment {PaymentId}.", payment.Id);
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
            if (amountPaid != payment.Amount) 
            {
                logger.LogError("Verified Paystack amount did not match payment {PaymentId}.", payment.Id);
                throw new InvalidOperationException("Payment amount mismatch.");
            }
                

            // 5. Get booking
            var booking = await unitOfWork.Bookings.GetBookingById(payment.BookingId, cancellationToken);
            if (booking == null)
            {
                logger.LogError("Booking was not found for payment {PaymentId}.", payment.Id);
                throw new InvalidOperationException("Booking not found.");
            }

            // 🔒 Extra validation (VERY IMPORTANT)
            if (booking.Amount != payment.Amount)
            {
                logger.LogError("Booking amount did not match payment {PaymentId}.", payment.Id);
                throw new InvalidOperationException("Booking amount mismatch.");
            }

            var transactionId = verification.Data.Id?.ToString() ?? reference;
            payment.MarkAsPaid(verification.Data.Authorization.AuthorizationCode, transactionId);
            logger.LogInformation("Payment {PaymentId} marked as paid.", payment.Id);

            booking.MarkAsPaid();
            logger.LogInformation("Booking {BookingId} marked as paid.", booking.Id);

            await escrowService.HoldPaymentInEscrowAsync(booking, payment, cancellationToken);

            await unitOfWork.Payments.UpdatePayment(payment, cancellationToken);
            await unitOfWork.Bookings.UpdateBooking(booking, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notificationService.PaymentConfirmedAsync(booking, cancellationToken);

            return Unit.Value;
        }

        private async Task ProcessTransferWebhookAsync(PaystackWebhookDto payload, CancellationToken cancellationToken)
        {
            var data = payload.Data;
            if (data == null || string.IsNullOrWhiteSpace(data.Reference))
                throw new InvalidOperationException("Paystack transfer webhook did not include a reference.");

            var escrow = await unitOfWork.Escrows.GetByTransferReference(data.Reference, cancellationToken);
            if (escrow == null)
            {
                logger.LogWarning("Paystack transfer webhook did not match a current payout attempt.");
                return;
            }

            var expectedMinorUnits = decimal.Round(escrow.WorkerShare * 100m, 0, MidpointRounding.AwayFromZero);
            if (data.Amount != expectedMinorUnits)
                throw new InvalidOperationException("Paystack transfer amount mismatch.");

            var providerStatus = payload.Event switch
            {
                "transfer.success" => "success",
                "transfer.reversed" => "reversed",
                _ => "failed"
            };

            if (providerStatus == "success")
                escrow.RecordPayoutTransfer(data.TransferCode ?? string.Empty, providerStatus, "PaystackWebhook");
            else
                escrow.MarkPayoutRetryable(providerStatus, "PaystackWebhook");

            await unitOfWork.Escrows.UpdateEscrow(escrow, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Applied Paystack transfer event {Event} for escrow {EscrowId}.", payload.Event, escrow.Id);
        }

        private static bool IsValidSignature(string payload, string signature, string secret)
        {
            if (string.IsNullOrWhiteSpace(signature))
                return false;

            try
            {
                var expected = Convert.FromHexString(ComputeHash(payload, secret));
                var supplied = Convert.FromHexString(signature);
                return CryptographicOperations.FixedTimeEquals(expected, supplied);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string ComputeHash(string payload, string secret)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();

        }
    }


}
