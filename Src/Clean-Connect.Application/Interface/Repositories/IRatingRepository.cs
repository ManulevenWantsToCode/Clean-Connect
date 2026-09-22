using Clean_Connect.Domain.Entities;

namespace Clean_Connect.Application.Interface.Repositories
{
    public interface IRatingRepository
    {
        Task<bool> AddRating(Ratings ratings, CancellationToken cancellationToken);
        Task<IEnumerable<Ratings>> GetRatingsByWorkerId(Guid workerId, CancellationToken cancellationToken);
        Task<List<Ratings>> GetAllRatingsAsync(CancellationToken cancellationToken);
        Task<bool> ExistAsync(Guid bookingId, CancellationToken cancellationToken);
    }
}