using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Clean_Connect.Persistence.Repositories
{
    public class BookingRepository(ApplicationDbContext dbContext) : IBookingRepository
    {

        public async Task CreateBooking(Booking booking, CancellationToken cancellationToken)
        {
            await dbContext.AddAsync(booking, cancellationToken);
        }

        public async Task<Booking> GetBookingById(Guid bookingId, CancellationToken cancellationToken)
        {
            return await dbContext.Bookings
                 .Include(x => x.Client)
                .Include(x => x.Worker)
                .Include(x => x.ServiceType)
                .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);
        }

       
        public async Task<List<Booking>> GetAllBookings(CancellationToken cancellationToken)
        {
            return await dbContext.Bookings
                .Include(x => x.Client)
                .Include(x => x.Ratings)
                .Include(x => x.Worker)
                .Include(x => x.ServiceType)
                .ToListAsync(cancellationToken);
        }

      

        public async Task UpdateBooking(Booking booking, CancellationToken cancellationToken)
        {
            dbContext.Bookings.Update(booking);
        }

        public async Task DeleteBooking(Booking booking, CancellationToken cancellationToken)
        {
             dbContext.Bookings.Remove(booking);
        }

        public async Task<List<Booking>> GetExpiredBookingsAsync(CancellationToken cancellationToken)
        {
            var expirableStatuses = new[]
            {
                BookingStatus.Pending,
                BookingStatus.AcceptedAwaitingPayment,
                BookingStatus.MarkAsPaid,
                BookingStatus.AwaitingClientStartConfirmation,
                BookingStatus.InProgress,
                BookingStatus.AwaitingClientConfirmation
            };

            return await dbContext.Bookings
                .Include(x => x.Client)
                .Include(x => x.Worker)
                .Include(x => x.ServiceType)
                .Where(x => (expirableStatuses.Contains(x.BookingStatus) && x.DateOfService.Date < DateTime.UtcNow.Date)
                    || (x.BookingStatus == BookingStatus.Expired && x.PaymentStatus == PaymentStatus.Successful))
                .ToListAsync(cancellationToken);
        }
    }
}
