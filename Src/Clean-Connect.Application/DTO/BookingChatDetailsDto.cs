namespace Clean_Connect.Application.DTO
{
    public record BookingChatDetailsDto
    {
        public Guid BookingId { get; init; }
        public string Provider { get; init; } = string.Empty;
        public string ConversationId { get; init; } = string.Empty;
        public string CurrentUserProviderId { get; init; } = string.Empty;
        public string OtherParticipantProviderId { get; init; } = string.Empty;
        public string OtherParticipantName { get; init; } = string.Empty;
        public string ParticipantRole { get; init; } = string.Empty;
        public string AppId { get; init; } = string.Empty;
        public string AppKey { get; init; } = string.Empty;
        public string AccessToken { get; init; } = string.Empty;
        public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }
        public int AccessTokenTtlSeconds { get; init; }
        public DateTimeOffset? OtherParticipantLastReadAtUtc { get; init; }
        public string ChatType { get; init; } = "singleChat";
    }
}
