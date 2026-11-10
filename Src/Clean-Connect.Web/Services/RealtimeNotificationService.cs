using Clean_Connect.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Clean_Connect.Web.Services
{
    public class RealtimeNotificationService : IRealtimeNotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public RealtimeNotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyUserAsync(string? email, string title, string message, string type = "info", string? url = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            await _hubContext.Clients.Group(email.Trim().ToLowerInvariant()).SendAsync("bookingNotification", new
            {
                title,
                message,
                type,
                url,
                createdAt = DateTime.UtcNow
            });
        }

        public async Task NotifyAdminAsync(string title, string message, string type = "info", string? url = null)
        {
            await _hubContext.Clients.Group("admins").SendAsync("adminNotification", new
            {
                title,
                message,
                type,
                url,
                createdAt = DateTime.UtcNow
            });
        }

        public async Task SendMessageNotificationAsync(
            string? recipientEmail,
            Guid bookingId,
            string senderName,
            string messagePreview,
            int unreadCount,
            string messageId)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return;
            }

            await _hubContext.Clients.Group(recipientEmail.Trim().ToLowerInvariant()).SendAsync("messageNotification", new
            {
                bookingId = bookingId,
                senderName = senderName,
                messagePreview = messagePreview,
                unreadCount = unreadCount,
                messageId = messageId,
                url = $"/Booking/{bookingId}/Messages",
                createdAt = DateTime.UtcNow
            });
        }

        public async Task UpdateUnreadMessageCountAsync(
            string? userEmail,
            int unreadCount)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                return;
            }

            await _hubContext.Clients.Group(userEmail.Trim().ToLowerInvariant()).SendAsync("updateUnreadMessageCount", new
            {
                unreadCount = unreadCount
            });
        }

        public async Task NotifyConversationReadAsync(
            string? recipientEmail,
            Guid bookingId,
            string readerRole,
            DateTime readAtUtc)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return;
            }

            await _hubContext.Clients.Group(recipientEmail.Trim().ToLowerInvariant()).SendAsync("conversationRead", new
            {
                bookingId = bookingId,
                readerRole = readerRole,
                readAt = readAtUtc
            });
        }
    }
}
