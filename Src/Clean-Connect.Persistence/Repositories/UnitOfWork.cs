using Clean_Connect.Infrastructure.Context;
using Clean_Connect.Application.Interface.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clean_Connect.Persistence.Repositories
{
    public class UnitOfWork(ApplicationDbContext dbContext, IWorkerRepository workerRepository,IRatingRepository ratingRepository, IPaymentRepository paymentRepository, IBookingRepository bookingRepository, IBookingChatRepository bookingChatRepository, IClientRepository clientRepository, IServiceTypeRepository serviceTypeRepository, IWorkerBankDetailRepository workerBankDetailRepository, IEscrowRepository escrowRepository, ICouponRepository couponRepository, INotificationRepository notificationRepository) : IUnitOfWork
    {
       
        public IWorkerRepository Workers { get;  } = workerRepository;


        public IClientRepository Clients { get; } = clientRepository;

        public IRatingRepository Ratings { get; } = ratingRepository;

        public IPaymentRepository Payments { get; } = paymentRepository;

        public IBookingRepository Bookings { get; } = bookingRepository;

        public IBookingChatRepository BookingChats { get; } = bookingChatRepository;

        public IWorkerBankDetailRepository WorkerBankDetails { get; } = workerBankDetailRepository;

        public IEscrowRepository Escrows { get; } = escrowRepository;

        public ICouponRepository Coupons { get; } = couponRepository;

        public INotificationRepository Notifications { get; } = notificationRepository;

       
        public IServiceTypeRepository ServiceTypes { get; } = serviceTypeRepository;

        public async Task <int> SaveChangesAsync(CancellationToken cancellation)
        {
            try
            {
                return await dbContext.SaveChangesAsync(cancellation);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException &&
                postgresException.SqlState is PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.SerializationFailure)
            {
                throw new BookingConflictException();
            }
        }
    }
}
