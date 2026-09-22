using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clean_Connect.Persistence.Repositories
{
    public class UnitOfWork(ApplicationDbContext dbContext, IWorkerRepository workerRepository,IRatingRepository ratingRepository, IPaymentRepository paymentRepository, IBookingRepository bookingRepository, IClientRepository clientRepository, IServiceTypeRepository serviceTypeRepository, IWalletRepository walletRepository, IEscrowRepository escrowRepository, ICouponRepository couponRepository, INotificationRepository notificationRepository) : IUnitOfWork
    {
       
        public IWorkerRepository Workers { get;  } = workerRepository;


        public IClientRepository Clients { get; } = clientRepository;

        public IRatingRepository Ratings { get; } = ratingRepository;

        public IPaymentRepository Payments { get; } = paymentRepository;

        public IBookingRepository Bookings { get; } = bookingRepository;

        public IWalletRepository Wallets { get; } = walletRepository;

        public IEscrowRepository Escrows { get; } = escrowRepository;

        public ICouponRepository Coupons { get; } = couponRepository;

        public INotificationRepository Notifications { get; } = notificationRepository;

       
        public IServiceTypeRepository ServiceTypes { get; } = serviceTypeRepository;

        public async Task <int> SaveChangesAsync(CancellationToken cancellation)
        {
            return await dbContext.SaveChangesAsync(cancellation);
        }
    }
}
