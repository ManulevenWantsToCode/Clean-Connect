using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Entities;
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
        private readonly ITransactionManager transactionManager;
        private readonly ILogger<ConfirmPaymentCommandHandler> logger;

        public ConfirmPaymentCommandHandler(
            IUnitOfWork unitOfWork,
            IPaystackService paystackService,
            EscrowService escrowService,
            INotificationService notificationService,
            ITransactionManager transactionManager,
            ILogger<ConfirmPaymentCommandHandler> logger)
        {
            this.unitOfWork = unitOfWork;
            this.paystackService = paystackService;
            this.escrowService = escrowService;
            this.notificationService = notificationService;
            this.transactionManager = transactionManager;
            this.logger = logger;
        }

        public async Task<PaymentConfirmationResult> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
        {
            var payment = await unitOfWork.Payments.GetByReferenceAsync(request.Reference, cancellationToken);
            if (payment == null)
            {
                logger.LogWarning("Payment was not found during confirmation.");
                throw new ArgumentException("Payment was not found for the given reference.");
            }

            // Already processed (webhook may have completed first) -> idempotent success.
            if (payment.Status == PaymentStatus.Successful)
            {
                logger.LogInformation("Payment was already confirmed.");
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
            if (!verification.Status)
                throw new HttpRequestException("Payment status could not be verified with the provider.");
            var verificationData = verification.Data
                ?? throw new HttpRequestException("Payment status could not be verified with the provider.");

            PaymentVerification.EnsureMatches(payment, verificationData);

            if (!string.Equals(verificationData.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                var terminalFailure = string.Equals(verificationData.Status, "failed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(verificationData.Status, "abandoned", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(verificationData.Status, "reversed", StringComparison.OrdinalIgnoreCase);
                var currentStatus = payment.Status;
                if (terminalFailure)
                {
                    await transactionManager.ExecuteAsync(async ct =>
                    {
                        var current = await unitOfWork.Payments.GetFreshByReferenceAsync(request.Reference, ct);
                        if (current?.Status == PaymentStatus.Pending)
                        {
                            current.MarkAsFailed(verificationData.GatewayResponse ?? verification.Message, verificationData.Id?.ToString());
                            await unitOfWork.Payments.UpdatePayment(current, ct);
                        }
                        currentStatus = current?.Status ?? currentStatus;
                    }, cancellationToken);
                }
                logger.LogInformation("Paystack verification returned a non-success payment state.");
                return new PaymentConfirmationResult(
                    payment.BookingId,
                    payment.Id,
                    payment.Amount,
                    payment.PaymentReference,
                    payment.TransactionId,
                    currentStatus == PaymentStatus.Successful ? "success" : currentStatus == PaymentStatus.Failed ? "failed" : "pending",
                    currentStatus == PaymentStatus.Failed ? verificationData.GatewayResponse ?? verification.Message : null);
            }

            var transactionId = verificationData.Id?.ToString() ?? request.Reference;
            Booking? confirmedBooking = null;
            Payment? confirmedPayment = null;
            await transactionManager.ExecuteAsync(async ct =>
            {
                confirmedPayment = await unitOfWork.Payments.GetFreshByReferenceAsync(request.Reference, ct);
                if (confirmedPayment is null)
                    throw new InvalidOperationException("Payment no longer exists.");
                if (confirmedPayment.Status == PaymentStatus.Successful)
                    return;
                if (confirmedPayment.Status != PaymentStatus.Pending)
                    throw new InvalidOperationException("Payment is no longer eligible for confirmation.");

                confirmedBooking = await unitOfWork.Bookings.GetBookingById(confirmedPayment.BookingId, ct);
                if (confirmedBooking == null || confirmedBooking.Amount != confirmedPayment.Amount)
                    throw new InvalidOperationException("The booking does not match the payment.");

                confirmedPayment.MarkAsPaid(verificationData.Authorization?.AuthorizationCode ?? string.Empty, transactionId);
                confirmedBooking.MarkAsPaid();
                await escrowService.HoldPaymentInEscrowAsync(confirmedBooking, confirmedPayment, ct);
                await unitOfWork.Payments.UpdatePayment(confirmedPayment, ct);
                await unitOfWork.Bookings.UpdateBooking(confirmedBooking, ct);
                await unitOfWork.SaveChangesAsync(ct);
                await notificationService.PaymentConfirmedAsync(confirmedBooking, ct);
            }, cancellationToken);

            logger.LogInformation("Payment confirmation completed for booking {BookingId}.", payment.BookingId);

            return new PaymentConfirmationResult(
                confirmedPayment?.BookingId ?? payment.BookingId,
                confirmedPayment?.Id ?? payment.Id,
                confirmedPayment?.Amount ?? payment.Amount,
                confirmedPayment?.PaymentReference ?? payment.PaymentReference,
                transactionId,
                "success",
                null);
        }
    }
}
