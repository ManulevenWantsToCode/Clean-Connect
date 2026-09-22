using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;

namespace Clean_Connect.Application.Interface.Repositories
{
    public interface INotificationRepository
    {
        Task CreateAsync(Notification notification, CancellationToken cancellationToken);
        Task<List<Notification>> GetByTargetAsync(Guid targetId, NotificationAudience audience, CancellationToken cancellationToken);
        Task<int> GetUnreadCountAsync(Guid targetId, NotificationAudience audience, CancellationToken cancellationToken);
        Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken);
        Task MarkAllAsReadAsync(Guid targetId, NotificationAudience audience, CancellationToken cancellationToken);
    }
}