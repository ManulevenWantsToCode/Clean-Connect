namespace Clean_Connect.Application.Interface.Services
{
    public interface IMessageRealtimeNotificationService
    {
        Task SendMessageNotificationAsync(
            string? recipientEmail,
            Guid bookingId,
            string senderName,
            string messagePreview,
            int unreadCount,
            string messageId);

        Task UpdateUnreadMessageCountAsync(
            string? userEmail,
            int unreadCount);

        Task NotifyConversationReadAsync(
            string? recipientEmail,
            Guid bookingId,
            string readerRole,
            DateTime readAtUtc);
    }
}
