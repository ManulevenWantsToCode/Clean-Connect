using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Clean_Connect.Persistence.Repositories
{
    public class EscrowRepository(ApplicationDbContext dbContext) : IEscrowRepository
    {
        public async Task CreateEscrow(Escrow escrow, CancellationToken cancellationToken)
        {
            await dbContext.Escrows.AddAsync(escrow, cancellationToken);
        }

        public async Task<Escrow?> GetByBookingId(Guid bookingId, CancellationToken cancellationToken)
        {
            return await dbContext.Escrows
                .FirstOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);
        }

        public async Task<Escrow?> GetByPaymentId(Guid paymentId, CancellationToken cancellationToken)
        {
            return await dbContext.Escrows
                .FirstOrDefaultAsync(x => x.PaymentId == paymentId, cancellationToken);
        }

        public async Task<List<Escrow>> GetByWorkerId(Guid workerId, CancellationToken cancellationToken)
        {
            return await dbContext.Escrows
                .Where(x => x.WorkerId == workerId)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Escrow>> GetByStatus(EscrowStatus status, CancellationToken cancellationToken)
        {
            return await dbContext.Escrows
                .Where(x => x.Status == status)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Escrow>> GetAllEscrowsAsync(CancellationToken cancellationToken)
        {
            return await dbContext.Escrows.ToListAsync(cancellationToken);
        }

        public Task UpdateEscrow(Escrow escrow, CancellationToken cancellationToken)
        {
            dbContext.Escrows.Update(escrow);
            return Task.CompletedTask;
        }
    }
}
