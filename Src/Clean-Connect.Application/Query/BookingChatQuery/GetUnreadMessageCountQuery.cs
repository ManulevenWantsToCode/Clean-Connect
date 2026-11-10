using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Query.BookingChatQuery
{
    public record GetUnreadMessageCountQuery : IRequest<int>;

    public class GetUnreadMessageCountQueryHandler(
        IUnitOfWork repo,
        ICurrentUser currentUser,
        ILogger<GetUnreadMessageCountQueryHandler> logger)
        : IRequestHandler<GetUnreadMessageCountQuery, int>
    {
        public async Task<int> Handle(GetUnreadMessageCountQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.Email))
            {
                return 0;
            }

            try
            {
                var client = await repo.Clients.GetByEmail(currentUser.Email, cancellationToken);
                if (client != null)
                {
                    return await repo.BookingChats.GetUnreadCountForClientAsync(client.Id, cancellationToken);
                }

                var worker = await repo.Workers.GetByEmail(currentUser.Email, cancellationToken);
                if (worker != null)
                {
                    return await repo.BookingChats.GetUnreadCountForWorkerAsync(worker.Id, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to retrieve unread message count for {Email}", currentUser.Email);
            }

            return 0;
        }
    }
}
