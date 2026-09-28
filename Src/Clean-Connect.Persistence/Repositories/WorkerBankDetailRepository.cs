using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Clean_Connect.Persistence.Repositories
{
    public class WorkerBankDetailRepository(ApplicationDbContext dbContext) : IWorkerBankDetailRepository
    {
        public async Task CreateAsync(WorkerBankDetail bankDetail, CancellationToken cancellationToken)
        {
            await dbContext.WorkerBankDetails.AddAsync(bankDetail, cancellationToken);
        }

        public async Task<WorkerBankDetail?> GetActiveByWorkerIdAsync(Guid workerId, CancellationToken cancellationToken)
        {
            return await dbContext.WorkerBankDetails
                .FirstOrDefaultAsync(x => x.WorkerId == workerId && x.IsActive, cancellationToken);
        }

        public async Task<List<WorkerBankDetail>> GetAllActiveAsync(CancellationToken cancellationToken)
        {
            return await dbContext.WorkerBankDetails
                .Where(x => x.IsActive)
                .ToListAsync(cancellationToken);
        }

        public Task UpdateAsync(WorkerBankDetail bankDetail, CancellationToken cancellationToken)
        {
            dbContext.WorkerBankDetails.Update(bankDetail);
            return Task.CompletedTask;
        }
    }
}