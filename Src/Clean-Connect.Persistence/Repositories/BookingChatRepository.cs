using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Clean_Connect.Persistence.Repositories
{
    public class BookingChatRepository(ApplicationDbContext dbContext) : IBookingChatRepository
    {
        public async Task CreateBookingChat(BookingChat bookingChat, CancellationToken cancellationToken)
        {
            await dbContext.BookingChats.AddAsync(bookingChat, cancellationToken);
        }

        public async Task<BookingChat?> GetByBookingId(Guid bookingId, CancellationToken cancellationToken)
        {
            return await dbContext.BookingChats
                .Include(x => x.Booking)
                    .ThenInclude(b => b.Client)
                .Include(x => x.Booking)
                    .ThenInclude(b => b.Worker)
                .FirstOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);
        }

        public async Task<int> GetUnreadCountForClientAsync(Guid clientId, CancellationToken cancellationToken)
        {
            return await dbContext.BookingChats
                .Where(x => x.Booking.ClientId == clientId && !x.IsDeleted)
                .SumAsync(x => x.ClientUnreadCount, cancellationToken);
        }

        public async Task<int> GetUnreadCountForWorkerAsync(Guid workerId, CancellationToken cancellationToken)
        {
            return await dbContext.BookingChats
                .Where(x => x.Booking.WorkerId == workerId && !x.IsDeleted)
                .SumAsync(x => x.WorkerUnreadCount, cancellationToken);
        }

        public async Task<Dictionary<Guid, BookingChat>> GetByBookingIdsAsync(IEnumerable<Guid> bookingIds, CancellationToken cancellationToken)
        {
            var ids = bookingIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<Guid, BookingChat>();
            }

            return await dbContext.BookingChats
                .Where(x => ids.Contains(x.BookingId) && !x.IsDeleted)
                .ToDictionaryAsync(x => x.BookingId, x => x, cancellationToken);
        }
    }
}
