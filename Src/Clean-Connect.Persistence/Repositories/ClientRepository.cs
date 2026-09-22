using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Persistence.Repositories
{
    public class ClientRepository(ApplicationDbContext dbContext) : IClientRepository
    {
        public async Task CreateClient(Client client, CancellationToken cancellationToken)
        {
            await dbContext.Clients.AddAsync(client);
        }

        public async Task<Client> GetClientByName(string name, CancellationToken cancellationToken)
        {
            return await dbContext.Clients.FindAsync(name.Trim(), cancellationToken);
        }

public async Task<Client> GetByEmail(string email, CancellationToken cancellationToken)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return await dbContext.Clients
                .Include(c => c.Bookings)
                .ThenInclude(c => c.ServiceType)
                .Include(c => c.Bookings)
                .ThenInclude(c => c.Worker)
                .FirstOrDefaultAsync(x => x.Email.Value == normalized);
            
        }
        public async Task<Client> GetClientById(Guid clientId, CancellationToken cancellationToken)
        {
            return await dbContext.Clients
                .Include(c => c.Bookings)
                .ThenInclude(c => c.ServiceType)
                .Include(c => c.Bookings)
                .ThenInclude(c => c.Worker)
                .FirstOrDefaultAsync(x => x.Id == clientId);
        }


        
        public async Task<IEnumerable<Client>> GetAllClients(CancellationToken cancellationToken)
        {
            return await dbContext.Clients.ToListAsync(cancellationToken);
        }

        public async Task UpdateClient(Client client, CancellationToken cancellationToken)
        {
            dbContext.Clients.Update(client);
        }

        public async Task DeleteClient(Client client, CancellationToken cancellationToken)
        {
            dbContext.Clients.Remove(client);
        }

        public async Task<Client?> GetByReferralCodeAsync(string referralCode, CancellationToken cancellationToken)
        {
            return await dbContext.Clients.FirstOrDefaultAsync(x => x.ReferralCode == referralCode, cancellationToken);
        }
    }
}
