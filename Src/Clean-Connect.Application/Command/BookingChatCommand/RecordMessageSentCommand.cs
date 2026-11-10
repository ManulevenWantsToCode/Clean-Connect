using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.BookingChatCommand
{
    public record RecordMessageSentCommand(
        Guid BookingId,
        string MessageText,
        string? MessageId = null) : IRequest<MessageSentNotificationResult>;

    public class RecordMessageSentCommandValidator : AbstractValidator<RecordMessageSentCommand>
    {
        public RecordMessageSentCommandValidator()
        {
            RuleFor(x => x.BookingId)
                .NotEmpty()
                .WithMessage("BookingId is required.");

            RuleFor(x => x.MessageText)
                .NotEmpty()
                .WithMessage("MessageText cannot be empty.")
                .MaximumLength(1000)
                .WithMessage("MessageText cannot exceed 1000 characters.");
        }
    }

    public class RecordMessageSentCommandHandler(
        IUnitOfWork repo,
        ICurrentUser currentUser,
        IMessageRealtimeNotificationService realtimeNotificationService,
        ILogger<RecordMessageSentCommandHandler> logger)
        : IRequestHandler<RecordMessageSentCommand, MessageSentNotificationResult>
    {
        public async Task<MessageSentNotificationResult> Handle(RecordMessageSentCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.Email))
            {
                logger.LogWarning("RecordMessageSent denied because no authenticated user was found.");
                throw new UnauthorizedAccessException("You must be logged in to send message notifications.");
            }

            var booking = await repo.Bookings.GetBookingById(request.BookingId, cancellationToken)
                ?? throw new KeyNotFoundException($"Booking with ID {request.BookingId} not found.");

            var bookingChat = await repo.BookingChats.GetByBookingId(request.BookingId, cancellationToken)
                ?? throw new KeyNotFoundException($"BookingChat metadata for booking {request.BookingId} was not found.");

            var now = DateTime.UtcNow;
            var messageId = string.IsNullOrWhiteSpace(request.MessageId)
                ? Guid.NewGuid().ToString("N")
                : request.MessageId.Trim();

            var preview = request.MessageText.Trim();
            if (preview.Length > 120)
            {
                preview = preview.Substring(0, 117) + "...";
            }

            string senderName;
            string senderRole;
            string recipientEmail;
            Guid recipientTargetId;
            bool isRecipientWorker;

            var client = await repo.Clients.GetByEmail(currentUser.Email, cancellationToken);
            if (client != null && booking.ClientId == client.Id)
            {
                senderName = booking.Client?.FullName?.ToString() ?? "Client";
                senderRole = "Client";
                var worker = booking.Worker ?? await repo.Workers.GetWorkerById(booking.WorkerId, cancellationToken);
                recipientEmail = worker?.Email?.Value ?? string.Empty;
                recipientTargetId = booking.WorkerId;
                isRecipientWorker = true;

                bookingChat.RecordMessageFromClient(request.MessageText, now, currentUser.Email);
            }
            else
            {
                var worker = await repo.Workers.GetByEmail(currentUser.Email, cancellationToken);
                if (worker != null && booking.WorkerId == worker.Id)
                {
                    senderName = booking.Worker?.FullName?.ToString() ?? "Specialist";
                    senderRole = "Worker";
                    var clientEntity = booking.Client ?? await repo.Clients.GetClientById(booking.ClientId, cancellationToken);
                    recipientEmail = clientEntity?.Email?.Value ?? string.Empty;
                    recipientTargetId = booking.ClientId;
                    isRecipientWorker = false;

                    bookingChat.RecordMessageFromWorker(request.MessageText, now, currentUser.Email);
                }
                else
                {
                    logger.LogWarning(
                        "RecordMessageSent access denied. Email {Email} is not a participant on booking {BookingId}.",
                        currentUser.Email,
                        request.BookingId);
                    throw new UnauthorizedAccessException("Only the booking client and assigned worker can send messages in this chat.");
                }
            }

            await repo.SaveChangesAsync(cancellationToken);

            int recipientTotalUnread = isRecipientWorker
                ? await repo.BookingChats.GetUnreadCountForWorkerAsync(recipientTargetId, cancellationToken)
                : await repo.BookingChats.GetUnreadCountForClientAsync(recipientTargetId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(recipientEmail))
            {
                await realtimeNotificationService.SendMessageNotificationAsync(
                    recipientEmail,
                    booking.Id,
                    senderName,
                    preview,
                    recipientTotalUnread,
                    messageId);
            }

            return new MessageSentNotificationResult
            {
                Success = true,
                BookingId = booking.Id,
                SenderName = senderName,
                SenderRole = senderRole,
                RecipientEmail = recipientEmail,
                RecipientUnreadCount = recipientTotalUnread,
                MessagePreview = preview,
                MessageId = messageId
            };
        }
    }
}
