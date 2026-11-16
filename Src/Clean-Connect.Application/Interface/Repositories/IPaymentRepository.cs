using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;

namespace Clean_Connect.Application.Interface.Repositories
{
    public sealed class PaymentInitializationConflictException(string message = "A payment attempt is already in progress or the idempotency key was reused with different details.") : Exception(message);

    public interface IPaymentRepository
    {
        Task<(Payment Payment, bool Created)> CreatePaymentIdempotentlyAsync(Payment payment, CancellationToken cancellationToken);
        Task<Payment?> GetByBookingAndIdempotencyKeyAsync(Guid bookingId, string idempotencyKey, CancellationToken cancellationToken);
        Task<Payment?> GetPendingByBookingAsync(Guid bookingId, CancellationToken cancellationToken);
        Task CreatePayment(Payment payment, CancellationToken cancellationToken);
        Task DeletePayment(Guid paymentId, CancellationToken cancellationToken);
        Task<bool> ExistsByReferenceAsync(string reference, CancellationToken cancellationToken);
        Task<List<Payment>> GetAllPayments(CancellationToken cancellationToken);
        Task<Payment?> GetByReferenceAsync(string paymentReference, CancellationToken cancellationToken);
        Task<Payment?> GetFreshByReferenceAsync(string paymentReference, CancellationToken cancellationToken);
        Task<Payment?> GetByRefundReferenceAsync(string refundReference, CancellationToken cancellationToken);
        Task<List<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken);
        Task<Payment?> GetPaymentById(Guid paymentId, CancellationToken cancellationToken);
        Task<List<Payment>> GetPaymentsByBookingId(Guid bookingId, CancellationToken cancellationToken);
        Task UpdatePayment(Payment payment, CancellationToken cancellationToken);
    }
}
