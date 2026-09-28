using Clean_Connect.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Clean_Connect.Web.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly IWorkerPresenceService _presence;

        public NotificationHub(IWorkerPresenceService presence)
        {
            _presence = presence;
        }

        public override async Task OnConnectedAsync()
        {
            var email = Context.User?.Identity?.Name?.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(email))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, email);
            }

            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
            }

            if (Context.User?.IsInRole("Worker") == true)
            {
                _presence.MarkOnline(email!);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var email = Context.User?.Identity?.Name?.Trim().ToLowerInvariant();

            if (Context.User?.IsInRole("Worker") == true && !string.IsNullOrWhiteSpace(email))
            {
                _presence.MarkOffline(email);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
