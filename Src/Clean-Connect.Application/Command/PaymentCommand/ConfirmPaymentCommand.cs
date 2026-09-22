using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.PaymentCommand
{
    public record ConfirmPaymentCommand(string Reference) : IRequest<PaymentConfirmationResult>;

    public class ConfirmPaymentCommandHandler : IRequestHandler<ConfirmPaymentCommand, PaymentConfirmationResult>
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IPaystackService paystackService;
        private readonly EscrowService escrowService;
        private readonly INotificationService notificationService;
        private readonly ILogger<ConfirmPaymentCommandHandler> logger;

        public ConfirmPaymentCommandHandler(
            IUnitOfWork unitOfWork,
            IPaystackService paystackService,
            EscrowService escrowService,
            INotificationService notificationService,
            ILogger<ConfirmPaymentCommandHandler> logger)
        {
            this.unitOfWork = unitOfWork;
            this.paystackService = paystackService;
            this.escrowService = escrowService;
            this.notificationService = notificationService;
            this.logger = logger;
        }

        public async Task<PaymentConfirmationResult> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
        {
            var payment = await unitOfWork.Payments.GetByReferenceAsync(request.Reference, cancellationToken);
            if (payment == null)
            {
                logger.LogWarning("Payment with reference {Reference} not found during confirmation.", request.Reference);
                throw new ArgumentException("Payment was not found for the given reference.");
            }

            // Already processed (webhook may have completed first) -> idempotent success.
            if (payment.Status == PaymentStatus.Successful)
            {
                logger.LogInformation("Payment {Reference} was already confirmed.", request.Reference);
                return new PaymentConfirmationResult(
                    payment.BookingId,
                    payment.Id,
                    payment.Amount,
                    payment.PaymentReference,
                    payment.TransactionId,
                    "success",
                    null);
            }

            var verification = await paystackService.VerifyTransaction(request.Reference);
            var amountPaid = verification.Data?.Amount / 100m ?? 0m;

            if (!verification.Status || verification.Data?.Status != "success")
            {
                payment.MarkAsFailed(
                    verification.Data?.GatewayResponse ?? verification.Message,
                    verification.Data?.Id?.ToString());
                await unitOfWork.Payments.UpdatePayment(payment, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogWarning("Payment {Reference} failed Paystack verification.", request.Reference);
                return new PaymentConfirmationResult(
                    payment.BookingId,
                    payment.Id,
                    payment.Amount,
                    payment.PaymentReference,
                    payment.TransactionId,
                    "failed",
                    verification.Data?.GatewayResponse ?? verification.Message);
            }

            if (amountPaid != payment.Amount)
            {
                logger.LogError(
                    "Amount mismatch for reference {Reference}. Paid {AmountPaid}, expected {ExpectedAmount}.",
                    request.Reference,
                    amountPaid,
                    payment.Amount);
                throw new InvalidOperationException("The amount paid does not match the booking amount.");
            }

            var booking = await unitOfWork.Bookings.GetBookingById(payment.BookingId, cancellationToken);
            if (booking == null)
            {
                logger.LogError("Booking for payment {Reference} was not found.", request.Reference);
                throw new InvalidOperationException("The booking for this payment was not found.");
            }

            if (booking.Amount != payment.Amount)
            {
                logger.LogError("Booking amount mismatch for reference {Reference}.", request.Reference);
                throw new InvalidOperationException("The booking amount does not match the payment amount.");
            }

            var transactionId = verification.Data?.Id?.ToString() ?? request.Reference;
            payment.MarkAsPaid(verification.Data?.Authorization?.AuthorizationCode ?? string.Empty, transactionId);
            booking.MarkAsPaid();

            await escrowService.HoldPaymentInEscrowAsync(booking, payment, cancellationToken);

            await unitOfWork.Payments.UpdatePayment(payment, cancellationToken);
            await unitOfWork.Bookings.UpdateBooking(booking, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notificationService.PaymentConfirmedAsync(booking, cancellationToken);

            logger.LogInformation("Payment {Reference} confirmed successfully.", request.Reference);

            return new PaymentConfirmationResult(
                payment.BookingId,
                payment.Id,
                payment.Amount,
                payment.PaymentReference,
                transactionId,
                "success",
                null);
        }
    }
}