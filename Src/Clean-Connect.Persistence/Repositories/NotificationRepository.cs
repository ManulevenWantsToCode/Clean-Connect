using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Clean_Connect.Persistence.Repositories
{
    public class NotificationRepository(ApplicationDbContext dbContext) : INotificationRepository
    {
        public async Task CreateAsync(Notification notification, CancellationToken cancellationToken)
        {
            await dbContext.Notifications.AddAsync(notification, cancellationToken);
        }

        public async Task<List<Notification>> GetByTargetAsync(Guid targetId, NotificationAudience audience, CancellationToken cancellationToken)
        {
            return await dbContext.Notifications
                .Where(n => n.TargetId == targetId && n.Audience == audience && !n.IsDeleted)
                .OrderByDescending(n => n.DateCreated)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetUnreadCountAsync(Guid targetId, NotificationAudience audience, CancellationToken cancellationToken)
        {
            return await dbContext.Notifications
                .CountAsync(n => n.TargetId == targetId && n.Audience == audience && !n.IsRead && !n.IsDeleted, cancellationToken);
        }

        public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken)
        {
            var notification = await dbContext.Notifications
                .Where(n => n.Id == notificationId && !n.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (notification == null)
                return;

            notification.MarkAsRead();
            dbContext.Notifications.Update(notification);
        }

        public async Task MarkAllAsReadAsync(Guid targetId, NotificationAudience audience, CancellationToken cancellationToken)
        {
            var notifications = await dbContext.Notifications
                .Where(n => n.TargetId == targetId && n.Audience == audience && !n.IsRead && !n.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var notification in notifications)
            {
                notification.MarkAsRead();
            }
        }
    }
}