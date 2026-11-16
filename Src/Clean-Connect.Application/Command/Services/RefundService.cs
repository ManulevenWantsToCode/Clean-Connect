using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.Services
{
    public sealed record RefundRequestResult(bool Accepted, string Status, string Message);

    public class RefundService
    {
        private readonly IUnitOfWork _repo;
        private readonly IPaystackService _paystackService;
        private readonly ILogger<RefundService> _logger;

        public RefundService(IUnitOfWork repo, IPaystackService paystackService, ILogger<RefundService> logger)
        {
            _repo = repo;
            _paystackService = paystackService;
            _logger = logger;
        }

        public async Task<bool> TryRefundExpiredBookingAsync(Booking booking, CancellationToken cancellationToken)
        {
            var result = await RequestRefundAsync(booking, cancellationToken);
            return result.Status == "Processed";
        }

        public async Task<RefundRequestResult> RequestRefundAsync(Booking booking, CancellationToken cancellationToken)
        {
            if (booking.PaymentStatus != PaymentStatus.Successful)
                return new(false, "NotRefundable", "The booking does not have a successful payment.");

            var escrow = await _repo.Escrows.GetByBookingId(booking.Id, cancellationToken);
            if (escrow == null || escrow.Status != EscrowStatus.Held)
            {
                _logger.LogInformation("Booking {BookingId} has no held escrow to refund.", booking.Id);
                return new(false, "NotRefundable", "No held escrow is available for this refund.");
            }

            var payments = await _repo.Payments.GetPaymentsByBookingId(booking.Id, cancellationToken);
            var payment = payments.FirstOrDefault(p => p != null && p.Status == PaymentStatus.Successful);
            if (payment == null)
            {
                _logger.LogWarning("No successful payment found to refund for booking {BookingId}.", booking.Id);
                return new(false, "NotRefundable", "No successful payment was found for this booking.");
            }

            var transactionReference = string.IsNullOrWhiteSpace(payment.TransactionId)
                ? payment.PaymentReference!
                : payment.TransactionId!;

            if (payment.Status == PaymentStatus.Refunded || payment.RefundStatus == "Processed")
                return new(true, "AlreadyProcessed", "The refund is already complete.");
            if (payment.RefundStatus is "Pending" or "Processing" or "NeedsAttention")
                return new(payment.RefundStatus != "NeedsAttention", payment.RefundStatus, "The existing refund attempt must be reconciled before another request.");

            var refundReference = payment.RefundReference ?? $"refund-{payment.Id:N}";
            payment.BeginRefund(refundReference, payment.Amount);
            await _repo.Payments.UpdatePayment(payment, cancellationToken);

            bool refunded;
            try
            {
                refunded = await _paystackService.RefundTransactionAsync(transactionReference, payment.RefundAmount, cancellationToken, refundReference, payment.Currency);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Refund call failed for booking {BookingId} ({ExceptionType}); the same provider reference will be reconciled.", booking.Id, ex.GetType().Name);
                payment.MarkRefundPending("Unknown");
                await _repo.Payments.UpdatePayment(payment, cancellationToken);
                return new(true, "ReconciliationRequired", "The provider outcome is unknown; the same refund reference will be reconciled.");
            }

            if (!refunded)
            {
                _logger.LogError("Paystack did not accept refund attempt for booking {BookingId}; manual reconciliation is required.", booking.Id);
                payment.MarkRefundPending("NeedsAttention");
                await _repo.Payments.UpdatePayment(payment, cancellationToken);
                return new(false, "NeedsAttention", "Paystack requires operator attention before this refund can continue.");
            }

            payment.MarkRefundPending("Pending");
            await _repo.Payments.UpdatePayment(payment, cancellationToken);
            _logger.LogInformation("Paystack accepted refund reference for booking {BookingId}; awaiting provider confirmation.", booking.Id);
            return new(true, "Pending", "Paystack accepted the refund request; final confirmation is pending.");
        }
    }
}
