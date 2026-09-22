using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Persistence.Repositories
{
    public class ServiceTypeRepository(ApplicationDbContext context) : IServiceTypeRepository
    {
        public async Task AddAsync(ServiceType serviceType, CancellationToken cancellationToken)
        {
            await context.ServiceTypes.AddAsync(serviceType);

        }

        public async Task<bool> CheckExistingByName(string name, CancellationToken cancellationToken)
        {
            return await context.ServiceTypes.AnyAsync(s => s.Name.ToLower() == name.ToLower());
        }

        //public async Task<List<ServiceType>> GetByName(string name, CancellationToken cancellationToken)
        //{
        //    return await context.ServiceTypes.Where(s => s.Name.ToLower() == name.ToLower()).ToListAsync();

        //}
        public async Task AddRangeAsync(IEnumerable<ServiceType> serviceTypes, CancellationToken cancellationToken)
        {
            await context.ServiceTypes.AddRangeAsync(serviceTypes);
        }

        public async Task DeleteAsync(ServiceType serviceType, CancellationToken cancellationToken)
        {
            context.ServiceTypes.Remove(serviceType);
            await Task.CompletedTask;
        }
        public async Task UpdateAsync(ServiceType serviceType, CancellationToken cancellationToken)
        {
            context.ServiceTypes.Update(serviceType);
            await Task.CompletedTask;
        }

        public async Task<ServiceType> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return await context.ServiceTypes.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        }

        public async Task<List<ServiceType>> GetAllAsync(CancellationToken cancellationToken)
        {
            return await context.ServiceTypes.Where(s => !s.IsDeleted).ToListAsync(cancellationToken);
        }

        public async Task<List<ServiceType>> GetAllIncludingDeletedAsync(CancellationToken cancellationToken)
        {
            return await context.ServiceTypes.ToListAsync(cancellationToken);
        }
    }
}
