using Clean_Connect.Domain.Utilities;

namespace Clean_Connect.Domain.Entities
{
    public class BookingChat : BaseEntity
    {
        public const string AgoraProvider = "Agora";

        private BookingChat() { }

        private BookingChat(
            Guid bookingId,
            Guid clientId,
            Guid workerId,
            string? createdBy = null)
        {
            ValidateId(bookingId, nameof(bookingId));
            ValidateId(clientId, nameof(clientId));
            ValidateId(workerId, nameof(workerId));

            BookingId = bookingId;
            Provider = AgoraProvider;
            ProviderConversationId = BuildBookingConversationId(bookingId);
            ClientProviderUserId = BuildClientUserId(clientId);
            WorkerProviderUserId = BuildWorkerUserId(workerId);
            UpdateMetadata(createdBy);
        }

        public Guid BookingId { get; private set; }
        public Booking Booking { get; private set; } = default!;
        public string Provider { get; private set; } = default!;
        public string ProviderConversationId { get; private set; } = default!;
        public string ClientProviderUserId { get; private set; } = default!;
        public string WorkerProviderUserId { get; private set; } = default!;

        public int ClientUnreadCount { get; private set; }
        public int WorkerUnreadCount { get; private set; }
        public string? LastMessageText { get; private set; }
        public string? LastMessageSender { get; private set; }
        public DateTime? LastMessageSentAt { get; private set; }
        public DateTime? ClientLastReadAt { get; private set; }
        public DateTime? WorkerLastReadAt { get; private set; }

        public static BookingChat CreateForBooking(
            Guid bookingId,
            Guid clientId,
            Guid workerId,
            string? createdBy = null)
        {
            return new BookingChat(bookingId, clientId, workerId, createdBy);
        }

        public void RecordMessageFromClient(string messageText, DateTime sentAt, string? modifiedBy = null)
        {
            WorkerUnreadCount++;
            LastMessageText = TruncatePreview(messageText);
            LastMessageSender = "Client";
            LastMessageSentAt = sentAt;
            ClientLastReadAt = sentAt;
            UpdateMetadata(modifiedBy);
        }

        public void RecordMessageFromWorker(string messageText, DateTime sentAt, string? modifiedBy = null)
        {
            ClientUnreadCount++;
            LastMessageText = TruncatePreview(messageText);
            LastMessageSender = "Worker";
            LastMessageSentAt = sentAt;
            WorkerLastReadAt = sentAt;
            UpdateMetadata(modifiedBy);
        }

        public void MarkAsReadByClient(string? modifiedBy = null)
        {
            ClientUnreadCount = 0;
            ClientLastReadAt = DateTime.UtcNow;
            UpdateMetadata(modifiedBy);
        }

        public void MarkAsReadByWorker(string? modifiedBy = null)
        {
            WorkerUnreadCount = 0;
            WorkerLastReadAt = DateTime.UtcNow;
            UpdateMetadata(modifiedBy);
        }

        public void ReassignWorker(Guid workerId, string? modifiedBy = null)
        {
            ValidateId(workerId, nameof(workerId));
            WorkerProviderUserId = BuildWorkerUserId(workerId);
            WorkerUnreadCount = 0;
            WorkerLastReadAt = null;
            UpdateMetadata(modifiedBy);
        }

        private static string? TruncatePreview(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var trimmed = text.Trim();
            return trimmed.Length > 500 ? trimmed.Substring(0, 500) : trimmed;
        }

        public static string BuildBookingConversationId(Guid bookingId)
        {
            ValidateId(bookingId, nameof(bookingId));
            return $"booking-{bookingId:N}";
        }

        public static string BuildClientUserId(Guid clientId)
        {
            ValidateId(clientId, nameof(clientId));
            return $"client-{clientId:N}";
        }

        public static string BuildWorkerUserId(Guid workerId)
        {
            ValidateId(workerId, nameof(workerId));
            return $"worker-{workerId:N}";
        }

        private static void ValidateId(Guid id, string paramName)
        {
            if (id == Guid.Empty)
                throw new ArgumentException($"{paramName} cannot be empty.", paramName);
        }
    }
}
