namespace Clean_Connect.Application.DTO
{
    public record MessageSentNotificationResult
    {
        public bool Success { get; init; }
        public Guid BookingId { get; init; }
        public string SenderName { get; init; } = string.Empty;
        public string SenderRole { get; init; } = string.Empty;
        public string RecipientEmail { get; init; } = string.Empty;
        public int RecipientUnreadCount { get; init; }
        public string MessagePreview { get; init; } = string.Empty;
        public string MessageId { get; init; } = string.Empty;
    }
}
