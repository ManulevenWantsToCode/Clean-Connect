using Clean_Connect.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Interface.Repositories
{
    public interface IServiceTypeRepository
    {
        Task AddAsync(ServiceType serviceType, CancellationToken cancellationToken);

        Task<bool> CheckExistingByName(string name, CancellationToken cancellationToken);

        Task AddRangeAsync(IEnumerable<ServiceType> serviceTypes, CancellationToken cancellationToken);

        Task DeleteAsync(ServiceType serviceType, CancellationToken cancellationToken);

        Task UpdateAsync(ServiceType serviceType, CancellationToken cancellationToken);

        //Task<List<ServiceType>> GetByName(string name, CancellationToken cancellationToken);
        Task<ServiceType> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        Task<List<ServiceType>> GetAllAsync(CancellationToken cancellationToken);
        Task<List<ServiceType>> GetAllIncludingDeletedAsync(CancellationToken cancellationToken);
    }
}
