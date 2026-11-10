using Clean_Connect.Application.Interface.Services;

namespace Clean_Connect.Application.Command.Services
{
    public class NullMessageRealtimeNotificationService : IMessageRealtimeNotificationService
    {
        public Task SendMessageNotificationAsync(
            string? recipientEmail,
            Guid bookingId,
            string senderName,
            string messagePreview,
            int unreadCount,
            string messageId)
        {
            return Task.CompletedTask;
        }

        public Task UpdateUnreadMessageCountAsync(
            string? userEmail,
            int unreadCount)
        {
            return Task.CompletedTask;
        }

        public Task NotifyConversationReadAsync(
            string? recipientEmail,
            Guid bookingId,
            string readerRole,
            DateTime readAtUtc)
        {
            return Task.CompletedTask;
        }
    }
}
