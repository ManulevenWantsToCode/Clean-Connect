using Clean_Connect.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Interface.Repositories
{
    public interface IBookingRepository
    {
        Task CreateBooking(Booking booking, CancellationToken cancellationToken);

        Task<Booking> GetBookingById(Guid bookingId, CancellationToken cancellationToken);

        Task<List<Booking>> GetAllBookings(CancellationToken cancellationToken);

        Task UpdateBooking(Booking booking, CancellationToken cancellationToken);

        Task DeleteBooking(Booking booking, CancellationToken cancellationToken);

        Task<List<Booking>> GetExpiredBookingsAsync(CancellationToken cancellationToken);

    }
}
