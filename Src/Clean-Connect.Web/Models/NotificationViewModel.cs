namespace Clean_Connect.Web.Models
{
    public class NotificationPageViewModel
    {
        public string Audience { get; set; } = "User";
        public int NeedsAttentionCount { get; set; }
        public List<NotificationItemViewModel> Items { get; set; } = new();
    }

    public class NotificationItemViewModel
    {
        public Guid NotificationId { get; set; }
        public bool IsRead { get; set; }
        public Guid BookingId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tone { get; set; } = "info";
        public string Icon { get; set; } = "fa-bell";
        public string ActionText { get; set; } = "View";
        public string? ActionUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool NeedsAttention { get; set; }
    }
}
