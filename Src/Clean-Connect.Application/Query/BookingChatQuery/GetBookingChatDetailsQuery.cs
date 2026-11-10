using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Query.BookingChatQuery
{
    public record GetBookingChatDetailsQuery(Guid BookingId) : IRequest<BookingChatDetailsDto>;

    public class GetBookingChatDetailsQueryValidator : AbstractValidator<GetBookingChatDetailsQuery>
    {
        public GetBookingChatDetailsQueryValidator()
        {
            RuleFor(x => x.BookingId)
                .NotEmpty()
                .WithMessage("BookingId is required.");
        }
    }

    public class GetBookingChatDetailsQueryHandler(
        IUnitOfWork repo,
        ICurrentUser currentUser,
        IAgoraChatTokenService agoraChatTokenService,
        IAgoraChatProvisioningService agoraChatProvisioningService,
        ILogger<GetBookingChatDetailsQueryHandler> logger)
        : IRequestHandler<GetBookingChatDetailsQuery, BookingChatDetailsDto>
    {
        public async Task<BookingChatDetailsDto> Handle(GetBookingChatDetailsQuery request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.Email))
            {
                logger.LogWarning("Booking chat access denied because no authenticated user was found.");
                throw new UnauthorizedAccessException("You must be logged in to access booking chat.");
            }

            var booking = await repo.Bookings.GetBookingById(request.BookingId, cancellationToken)
                ?? throw new KeyNotFoundException($"Booking with ID {request.BookingId} not found.");

            var bookingChat = await repo.BookingChats.GetByBookingId(request.BookingId, cancellationToken)
                ?? throw new KeyNotFoundException($"Chat metadata for booking {request.BookingId} was not found.");

            var client = await repo.Clients.GetByEmail(currentUser.Email, cancellationToken);
            if (client != null && booking.ClientId == client.Id)
            {
                return await BuildDetailsAsync(
                    booking.Id,
                    bookingChat.Provider,
                    bookingChat.ProviderConversationId,
                    bookingChat.ClientProviderUserId,
                    bookingChat.WorkerProviderUserId,
                    booking.Worker?.FullName?.ToString() ?? "Worker",
                    "Client",
                    bookingChat.WorkerLastReadAt,
                    cancellationToken);
            }

            var worker = await repo.Workers.GetByEmail(currentUser.Email, cancellationToken);
            if (worker != null && booking.WorkerId == worker.Id)
            {
                return await BuildDetailsAsync(
                    booking.Id,
                    bookingChat.Provider,
                    bookingChat.ProviderConversationId,
                    bookingChat.WorkerProviderUserId,
                    bookingChat.ClientProviderUserId,
                    booking.Client?.FullName?.ToString() ?? "Client",
                    "Worker",
                    bookingChat.ClientLastReadAt,
                    cancellationToken);
            }

            logger.LogWarning(
                "Booking chat access denied for a non-participant on booking {BookingId}.",
                request.BookingId);

            throw new UnauthorizedAccessException("Only the booking client and assigned worker can access this chat.");
        }

        private async Task<BookingChatDetailsDto> BuildDetailsAsync(
            Guid bookingId,
            string provider,
            string conversationId,
            string currentUserProviderId,
            string otherParticipantProviderId,
            string otherParticipantName,
            string participantRole,
            DateTime? otherParticipantLastReadAt,
            CancellationToken cancellationToken)
        {
            await agoraChatProvisioningService.EnsureUsersExistAsync(
                [currentUserProviderId, otherParticipantProviderId],
                cancellationToken);

            var token = agoraChatTokenService.CreateUserToken(currentUserProviderId);

            return new BookingChatDetailsDto
                {
                    BookingId = bookingId,
                    Provider = provider,
                    ConversationId = conversationId,
                    CurrentUserProviderId = currentUserProviderId,
                    OtherParticipantProviderId = otherParticipantProviderId,
                    OtherParticipantName = otherParticipantName,
                    ParticipantRole = participantRole,
                    AppId = token.AppId,
                    AppKey = token.AppKey,
                    AccessToken = token.AccessToken,
                    AccessTokenExpiresAtUtc = token.ExpiresAtUtc,
                    AccessTokenTtlSeconds = token.TtlSeconds,
                    OtherParticipantLastReadAtUtc = otherParticipantLastReadAt.HasValue
                        ? new DateTimeOffset(DateTime.SpecifyKind(otherParticipantLastReadAt.Value, DateTimeKind.Utc))
                        : null
                };
        }
    }
}
