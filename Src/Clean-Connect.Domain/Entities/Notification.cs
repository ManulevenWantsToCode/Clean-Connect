using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Utilities;

namespace Clean_Connect.Domain.Entities
{
    public class Notification : BaseEntity
    {
        private Notification() { }

        private Notification(
            Guid targetId,
            Guid bookingId,
            NotificationAudience audience,
            string title,
            string message,
            string status,
            string tone,
            string icon,
            string actionText,
            string actionUrl,
            bool needsAttention,
            string createdBy)
        {
            TargetId = targetId;
            BookingId = bookingId;
            Audience = audience;
            Title = title;
            Message = message;
            Status = status;
            Tone = tone;
            Icon = icon;
            ActionText = actionText;
            ActionUrl = actionUrl;
            NeedsAttention = needsAttention;
            IsRead = false;
            UpdateMetadata(createdBy);
        }

        public Guid TargetId { get; private set; }
        public Guid BookingId { get; private set; }
        public NotificationAudience Audience { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Message { get; private set; } = string.Empty;
        public string Status { get; private set; } = string.Empty;
        public string Tone { get; private set; } = "info";
        public string Icon { get; private set; } = "fa-bell";
        public string ActionText { get; private set; } = "View";
        public string ActionUrl { get; private set; } = string.Empty;
        public bool NeedsAttention { get; private set; }
        public bool IsRead { get; private set; }
        public DateTime? ReadAt { get; private set; }

        public static Notification Create(
            Guid targetId,
            Guid bookingId,
            NotificationAudience audience,
            string title,
            string message,
            string status,
            string tone,
            string icon,
            string actionText,
            string actionUrl,
            bool needsAttention,
            string? createdBy = null)
        {
            return new Notification(
                targetId,
                bookingId,
                audience,
                title,
                message,
                status,
                tone,
                icon,
                actionText,
                actionUrl,
                needsAttention,
                createdBy ?? string.Empty);
        }

        public void MarkAsRead(string? modifiedBy = null)
        {
            if (IsRead)
                return;

            IsRead = true;
            ReadAt = DateTime.UtcNow;
            UpdateMetadata(modifiedBy);
        }
    }
}