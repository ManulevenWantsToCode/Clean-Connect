using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;

namespace Clean_Connect.Persistence.Repositories
{
    public class BookingRepository(ApplicationDbContext dbContext) : IBookingRepository
    {

        public async Task<BookingCreateResult> CreateBookingIdempotentlyAsync(Booking booking, BookingChat bookingChat, CancellationToken cancellationToken)
        {
            var existing = await FindIdempotentBookingAsync(booking.ClientId, booking.IdempotencyKey!, cancellationToken);
            if (existing is not null)
                return MatchIdempotency(existing, booking.RequestHash!);

            var transaction = dbContext.Database.CurrentTransaction;
            var ownsTransaction = transaction is null;
            if (ownsTransaction)
                transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            try
            {
                existing = await FindIdempotentBookingAsync(booking.ClientId, booking.IdempotencyKey!, cancellationToken);
                if (existing is not null)
                {
                    if (ownsTransaction)
                        await transaction!.CommitAsync(cancellationToken);
                    return MatchIdempotency(existing, booking.RequestHash!);
                }

                dbContext.Bookings.Add(booking);
                dbContext.BookingChats.Add(bookingChat);
                await dbContext.SaveChangesAsync(cancellationToken);
                if (ownsTransaction)
                    await transaction!.CommitAsync(cancellationToken);
                return new BookingCreateResult(booking.Id, true);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException)
            {
                if (ownsTransaction)
                    await transaction!.RollbackAsync(cancellationToken);
                dbContext.Entry(booking).State = EntityState.Detached;
                dbContext.Entry(bookingChat).State = EntityState.Detached;

                if (postgresException.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation)
                {
                    existing = await FindIdempotentBookingAsync(booking.ClientId, booking.IdempotencyKey!, cancellationToken);
                    if (existing is not null)
                        return MatchIdempotency(existing, booking.RequestHash!);
                }

                if (postgresException.SqlState is PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
                    throw new BookingConflictException();

                if (postgresException.SqlState == PostgresErrorCodes.UniqueViolation && postgresException.ConstraintName == "IX_Bookings_ClientId_IdempotencyKey")
                    throw new IdempotencyConflictException();

                throw;
            }
            finally
            {
                if (ownsTransaction && transaction is not null)
                    await transaction.DisposeAsync();
            }
        }

        public async Task<BookingCreateResult?> GetIdempotentBookingAsync(Guid clientId, string key, string requestHash, CancellationToken cancellationToken)
        {
            var existing = await FindIdempotentBookingAsync(clientId, key, cancellationToken);
            return existing is null ? null : MatchIdempotency(existing, requestHash);
        }

        private async Task<Booking?> FindIdempotentBookingAsync(Guid clientId, string key, CancellationToken cancellationToken) =>
            await dbContext.Bookings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ClientId == clientId && x.IdempotencyKey == key, cancellationToken);

        private static BookingCreateResult MatchIdempotency(Booking existing, string requestHash)
        {
            BookingIdempotency.EnsureSameRequest(existing.RequestHash!, requestHash);
            return new BookingCreateResult(existing.Id, false);
        }

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
