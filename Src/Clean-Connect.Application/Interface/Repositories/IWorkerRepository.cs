using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Interface.Repositories
{
    public interface IWorkerRepository
    {
        Task CreateWorker(Worker worker, CancellationToken cancellationToken);

        Task<Worker> GetWorkerById(Guid workerId, CancellationToken cancellationToken);

        Task<IEnumerable<Worker>> GetAllWorkers(CancellationToken cancellationToken);

        Task<List<Worker>> GetAllWorkersWithBookingsAsync(CancellationToken cancellationToken);

        Task UpdateWorker(Worker worker, CancellationToken cancellationToken);

        Task DeleteWorker(Worker worker, CancellationToken cancellationToken);

        Task<Worker> GetByEmail(string email, CancellationToken cancellationToken);
        Task<List<WorkerWithDistance>> GetNearByWorkersAsync(double latitude, double longitude, double radiusInMeters, Guid serviceType);

        Task<List<WorkerWithDistance>> GetNearbyWorkersWithBookingsAsync(double latitude, double longitude, double radiusInMeters, CancellationToken cancellationToken);

        Task<List<WorkerWithDistance>> GetAllWorkersWithDistanceAsync(double latitude, double longitude, CancellationToken cancellationToken);

        Task<List<WorkerWithDistance>> GetAvailableWorkersWithDistanceAsync(double? latitude, double? longitude, double? radiusInMeters, Guid? serviceTypeId, DateTime dateOfService, CancellationToken cancellationToken);


        Task<Worker> GetWorkerByName(string name, CancellationToken cancellationToken);
    }
}
