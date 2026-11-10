using Clean_Connect.Domain.Entities;

namespace Clean_Connect.Application.Interface.Repositories
{
    public interface IBookingChatRepository
    {
        Task CreateBookingChat(BookingChat bookingChat, CancellationToken cancellationToken);
        Task<BookingChat?> GetByBookingId(Guid bookingId, CancellationToken cancellationToken);
        Task<int> GetUnreadCountForClientAsync(Guid clientId, CancellationToken cancellationToken);
        Task<int> GetUnreadCountForWorkerAsync(Guid workerId, CancellationToken cancellationToken);
        Task<Dictionary<Guid, BookingChat>> GetByBookingIdsAsync(IEnumerable<Guid> bookingIds, CancellationToken cancellationToken);
    }
}
