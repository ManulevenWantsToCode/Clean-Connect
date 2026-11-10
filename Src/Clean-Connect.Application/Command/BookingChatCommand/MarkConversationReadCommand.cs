using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.BookingChatCommand
{
    public record MarkConversationReadCommand(Guid BookingId) : IRequest<int>;

    public class MarkConversationReadCommandValidator : AbstractValidator<MarkConversationReadCommand>
    {
        public MarkConversationReadCommandValidator()
        {
            RuleFor(x => x.BookingId)
                .NotEmpty()
                .WithMessage("BookingId is required.");
        }
    }

    public class MarkConversationReadCommandHandler(
        IUnitOfWork repo,
        ICurrentUser currentUser,
        IMessageRealtimeNotificationService realtimeNotificationService,
        ILogger<MarkConversationReadCommandHandler> logger)
        : IRequestHandler<MarkConversationReadCommand, int>
    {
        public async Task<int> Handle(MarkConversationReadCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.Email))
            {
                logger.LogWarning("MarkConversationRead denied because no authenticated user was found.");
                throw new UnauthorizedAccessException("You must be logged in to mark conversations as read.");
            }

            var booking = await repo.Bookings.GetBookingById(request.BookingId, cancellationToken)
                ?? throw new KeyNotFoundException($"Booking with ID {request.BookingId} not found.");

            var bookingChat = await repo.BookingChats.GetByBookingId(request.BookingId, cancellationToken)
                ?? throw new KeyNotFoundException($"BookingChat metadata for booking {request.BookingId} was not found.");

            int remainingCount = 0;
            string recipientEmail;
            string readerRole;
            DateTime readAtUtc;

            var client = await repo.Clients.GetByEmail(currentUser.Email, cancellationToken);
            if (client != null && booking.ClientId == client.Id)
            {
                bookingChat.MarkAsReadByClient(currentUser.Email);
                await repo.SaveChangesAsync(cancellationToken);
                remainingCount = await repo.BookingChats.GetUnreadCountForClientAsync(client.Id, cancellationToken);

                readerRole = "Client";
                readAtUtc = bookingChat.ClientLastReadAt ?? DateTime.UtcNow;
                var worker = booking.Worker ?? await repo.Workers.GetWorkerById(booking.WorkerId, cancellationToken);
                recipientEmail = worker?.Email?.Value ?? string.Empty;
            }
            else
            {
                var worker = await repo.Workers.GetByEmail(currentUser.Email, cancellationToken);
                if (worker != null && booking.WorkerId == worker.Id)
                {
                    bookingChat.MarkAsReadByWorker(currentUser.Email);
                    await repo.SaveChangesAsync(cancellationToken);
                    remainingCount = await repo.BookingChats.GetUnreadCountForWorkerAsync(worker.Id, cancellationToken);

                    readerRole = "Worker";
                    readAtUtc = bookingChat.WorkerLastReadAt ?? DateTime.UtcNow;
                    var clientEntity = booking.Client ?? await repo.Clients.GetClientById(booking.ClientId, cancellationToken);
                    recipientEmail = clientEntity?.Email?.Value ?? string.Empty;
                }
                else
                {
                    logger.LogWarning(
                        "MarkConversationRead access denied for a non-participant on booking {BookingId}.",
                        request.BookingId);
                    throw new UnauthorizedAccessException("Only the booking client and assigned worker can access this conversation.");
                }
            }

            await realtimeNotificationService.UpdateUnreadMessageCountAsync(currentUser.Email, remainingCount);
            await realtimeNotificationService.NotifyConversationReadAsync(recipientEmail, booking.Id, readerRole, readAtUtc);

            return remainingCount;
        }
    }
}
