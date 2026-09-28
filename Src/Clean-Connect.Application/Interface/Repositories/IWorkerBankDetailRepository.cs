using Clean_Connect.Domain.Entities;

namespace Clean_Connect.Application.Interface.Repositories
{
    public interface IWorkerBankDetailRepository
    {
        Task CreateAsync(WorkerBankDetail bankDetail, CancellationToken cancellationToken);

        Task<WorkerBankDetail?> GetActiveByWorkerIdAsync(Guid workerId, CancellationToken cancellationToken);

        Task<List<WorkerBankDetail>> GetAllActiveAsync(CancellationToken cancellationToken);

        Task UpdateAsync(WorkerBankDetail bankDetail, CancellationToken cancellationToken);
    }
}