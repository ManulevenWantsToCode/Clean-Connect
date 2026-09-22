using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;

namespace Clean_Connect.Application.Interface.Repositories
{
    public interface IEscrowRepository
    {
        Task CreateEscrow(Escrow escrow, CancellationToken cancellationToken);
        Task<Escrow?> GetByBookingId(Guid bookingId, CancellationToken cancellationToken);
        Task<Escrow?> GetByPaymentId(Guid paymentId, CancellationToken cancellationToken);
        Task<List<Escrow>> GetByWorkerId(Guid workerId, CancellationToken cancellationToken);
        Task<List<Escrow>> GetByStatus(EscrowStatus status, CancellationToken cancellationToken);
        Task<List<Escrow>> GetAllEscrowsAsync(CancellationToken cancellationToken);
        Task UpdateEscrow(Escrow escrow, CancellationToken cancellationToken);
    }
}