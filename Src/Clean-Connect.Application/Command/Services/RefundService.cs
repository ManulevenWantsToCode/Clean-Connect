using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.Services
{
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
            if (booking.PaymentStatus != PaymentStatus.Successful)
                return false;

            var escrow = await _repo.Escrows.GetByBookingId(booking.Id, cancellationToken);
            if (escrow == null || escrow.Status != EscrowStatus.Held)
            {
                _logger.LogInformation("Booking {BookingId} has no held escrow to refund.", booking.Id);
                return false;
            }

            var payments = await _repo.Payments.GetPaymentsByBookingId(booking.Id, cancellationToken);
            var payment = payments.FirstOrDefault(p => p != null && p.Status == PaymentStatus.Successful);
            if (payment == null)
            {
                _logger.LogWarning("No successful payment found to refund for booking {BookingId}.", booking.Id);
                return false;
            }

            var transactionReference = string.IsNullOrWhiteSpace(payment.TransactionId)
                ? payment.PaymentReference!
                : payment.TransactionId!;

            bool refunded;
            try
            {
                refunded = await _paystackService.RefundTransactionAsync(transactionReference, payment.Amount, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refund call failed for booking {BookingId}. Will retry on the next sweep.", booking.Id);
                return false;
            }

            if (!refunded)
            {
                _logger.LogError("Refund failed for booking {BookingId}. It will be retried on the next sweep.", booking.Id);
                return false;
            }

            payment.MarkAsRefunded($"Refunded because booking {booking.Id} expired before the job was completed.");
            escrow.MarkAsRefunded();
            booking.RefundPayment();

            await _repo.Payments.UpdatePayment(payment, cancellationToken);
            await _repo.Escrows.UpdateEscrow(escrow, cancellationToken);
            await _repo.Bookings.UpdateBooking(booking, cancellationToken);
            await _repo.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Refunded {Amount} for expired booking {BookingId}.", payment.Amount, booking.Id);
            return true;
        }
    }
}