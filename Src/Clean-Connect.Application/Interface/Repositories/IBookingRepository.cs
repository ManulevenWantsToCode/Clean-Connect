using Clean_Connect.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Interface.Repositories
{
    public sealed record BookingCreateResult(Guid BookingId, bool Created);

    public sealed class BookingConflictException(string message = "The worker is already booked for part of this time.") : Exception(message);

    public sealed class IdempotencyConflictException() : Exception("The Idempotency-Key was already used for a different booking request.");

    public static class BookingIdempotency
    {
        public static void EnsureSameRequest(string persistedHash, string incomingHash)
        {
            if (!string.Equals(persistedHash, incomingHash, StringComparison.Ordinal))
                throw new IdempotencyConflictException();
        }
    }

    public interface IBookingRepository
    {
        Task CreateBooking(Booking booking, CancellationToken cancellationToken);

        Task<BookingCreateResult?> GetIdempotentBookingAsync(Guid clientId, string key, string requestHash, CancellationToken cancellationToken);

        Task<BookingCreateResult> CreateBookingIdempotentlyAsync(Booking booking, BookingChat bookingChat, CancellationToken cancellationToken);

        Task<Booking> GetBookingById(Guid bookingId, CancellationToken cancellationToken);

        Task<List<Booking>> GetAllBookings(CancellationToken cancellationToken);

        Task UpdateBooking(Booking booking, CancellationToken cancellationToken);

        Task DeleteBooking(Booking booking, CancellationToken cancellationToken);

        Task<List<Booking>> GetExpiredBookingsAsync(CancellationToken cancellationToken);

    }
}
