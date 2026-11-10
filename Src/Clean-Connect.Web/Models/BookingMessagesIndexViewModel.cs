namespace Clean_Connect.Web.Models
{
    public record BookingMessagesIndexViewModel
    {
        public string ParticipantRole { get; init; } = string.Empty;
        public List<BookingMessageThreadItem> Threads { get; init; } = new();
    }

    public record BookingMessageThreadItem
    {
        public Guid BookingId { get; init; }
        public string ServiceName { get; init; } = string.Empty;
        public string OtherParticipantName { get; init; } = string.Empty;
        public string BookingStatus { get; init; } = string.Empty;
        public string PaymentStatus { get; init; } = string.Empty;
        public DateTime DateOfService { get; init; }
        public DateTime BookingDate { get; init; }
        public string TimeRange { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string? RejectionReason { get; init; }
        public int UnreadCount { get; init; }
        public string? LatestMessageText { get; init; }
        public DateTime? LatestMessageTime { get; init; }

        public string StatusPreview => GetStatusPreview();
        public string FormattedTime => GetFormattedTime();

        public string GetStatusPreview()
        {
            if (!string.IsNullOrWhiteSpace(LatestMessageText))
            {
                return LatestMessageText;
            }

            return BookingStatus switch
            {
                "Pending" => "Booking request submitted — awaiting specialist confirmation.",
                "AcceptedAwaitingPayment" => "Booking accepted — awaiting payment confirmation.",
                "MarkAsPaid" => $"Booking confirmed for {DateOfService:ddd, MMM d} — ready for arrival coordination.",
                "AwaitingClientStartConfirmation" => "Specialist is on site — awaiting start confirmation.",
                "InProgress" => "Cleaning service is in progress — live coordination active.",
                "AwaitingClientConfirmation" => "Job completed — awaiting your review and confirmation.",
                "Completed" => $"Service completed on {DateOfService:MMM d} — chat archived for your records.",
                "Rejected" => string.IsNullOrWhiteSpace(RejectionReason)
                    ? "Booking request was declined by the specialist."
                    : $"Booking declined: {RejectionReason}",
                "Cancelled" => "This booking was cancelled.",
                "Expired" => "This booking request has expired.",
                _ => $"Direct message thread for {ServiceName}."
            };
        }

        public string GetFormattedTime()
        {
            var dateToFormat = LatestMessageTime ?? BookingDate;
            var now = DateTime.UtcNow;
            var diff = now - dateToFormat;

            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24 && dateToFormat.Date == now.Date) return dateToFormat.ToString("h:mm tt");
            if (dateToFormat.Date == now.Date.AddDays(-1)) return "Yesterday";
            if (diff.TotalDays < 7) return dateToFormat.ToString("ddd");
            if (dateToFormat.Year == now.Year) return dateToFormat.ToString("MMM d");
            return dateToFormat.ToString("MMM d, yyyy");
        }
    }
}
