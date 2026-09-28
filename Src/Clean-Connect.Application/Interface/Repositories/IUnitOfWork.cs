using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Interface.Repositories
{
    public interface IUnitOfWork
    {
        IWorkerRepository Workers { get; } 
        IServiceTypeRepository ServiceTypes { get; }
        IClientRepository Clients { get; }
        IBookingRepository Bookings { get; }

        IPaymentRepository Payments { get; }
        IRatingRepository Ratings { get; }
        IWorkerBankDetailRepository WorkerBankDetails { get; }
        IEscrowRepository Escrows { get; }
        ICouponRepository Coupons { get; }
        INotificationRepository Notifications { get; }
        

        Task<int> SaveChangesAsync(CancellationToken cancellation);
    }
}
