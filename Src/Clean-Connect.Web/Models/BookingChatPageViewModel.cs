using Clean_Connect.Application.DTO;

namespace Clean_Connect.Web.Models
{
    public record BookingChatPageViewModel
    {
        public Guid BookingId { get; init; }
        public string ServiceName { get; init; } = string.Empty;
        public string OtherParticipantName { get; init; } = string.Empty;
        public string ParticipantRole { get; init; } = string.Empty;
        public string ConversationId { get; init; } = string.Empty;
        public string BookingStatus { get; init; } = string.Empty;
        public string PaymentStatus { get; init; } = string.Empty;
        public DateTime DateOfService { get; init; }
        public string SessionUrl { get; init; } = string.Empty;
        public string TimeRange { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public IReadOnlyList<BookingMessageThreadItem> Threads { get; init; } = Array.Empty<BookingMessageThreadItem>();

        public static BookingChatPageViewModel From(BookingDto booking, BookingChatDetailsDto chat, string sessionUrl)
        {
            return new BookingChatPageViewModel
            {
                BookingId = booking.Id,
                ServiceName = booking.ServiceName,
                OtherParticipantName = chat.OtherParticipantName,
                ParticipantRole = chat.ParticipantRole,
                ConversationId = chat.ConversationId,
                BookingStatus = booking.BookingStatus,
                PaymentStatus = booking.PaymentStatus,
                DateOfService = booking.DateOfService,
                SessionUrl = sessionUrl,
                TimeRange = booking.TimeRange ?? string.Empty,
                Address = booking.Address ?? string.Empty
            };
        }
    }
}
