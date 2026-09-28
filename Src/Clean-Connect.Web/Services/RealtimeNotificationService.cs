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
    }
}
