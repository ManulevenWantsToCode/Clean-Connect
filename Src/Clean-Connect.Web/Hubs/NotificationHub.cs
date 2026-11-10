using Clean_Connect.Application.Command.BookingChatCommand;
using Clean_Connect.Application.Query.BookingChatQuery;
using Clean_Connect.Web.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Clean_Connect.Web.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly IWorkerPresenceService _presence;
        private readonly IMediator _mediator;

        public NotificationHub(IWorkerPresenceService presence, IMediator mediator)
        {
            _presence = presence;
            _mediator = mediator;
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

        public async Task<int> GetUnreadMessageCount()
        {
            return await _mediator.Send(new GetUnreadMessageCountQuery());
        }

        public async Task<int> MarkConversationRead(Guid bookingId)
        {
            return await _mediator.Send(new MarkConversationReadCommand(bookingId));
        }

        public async Task<object> SendChatMessageNotification(Guid bookingId, string messageText, string? messageId = null)
        {
            return await _mediator.Send(new RecordMessageSentCommand(bookingId, messageText, messageId));
        }
    }
}
