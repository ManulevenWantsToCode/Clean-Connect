using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.ClientCommands
{
    public record ConfirmJobStartedCommand(Guid BookingId, Guid ClientId) : IRequest<bool>;

    public class ConfirmJobStartedValidator : AbstractValidator<ConfirmJobStartedCommand>
    {
        public ConfirmJobStartedValidator()
        {
            RuleFor(x => x.BookingId)
                .NotEmpty()
                .WithMessage("BookingId is required.");

            RuleFor(x => x.ClientId)
                .NotEmpty()
                .Must(id => id != Guid.Empty)
                .WithMessage("ClientId is required.");
        }
    }

    public class ConfirmJobStartedHandler : IRequestHandler<ConfirmJobStartedCommand, bool>
    {
        private readonly IUnitOfWork repo;
        private readonly INotificationService notificationService;
        private readonly ILogger<ConfirmJobStartedHandler> logger;

        public ConfirmJobStartedHandler(IUnitOfWork _repo, INotificationService notificationService, ILogger<ConfirmJobStartedHandler> _logger)
        {
            repo = _repo;
            this.notificationService = notificationService;
            logger = _logger;
        }

        public async Task<bool> Handle(ConfirmJobStartedCommand request, CancellationToken cancellationToken)
        {
            var booking = await repo.Bookings.GetBookingById(request.BookingId, cancellationToken);
            if (booking == null)
            {
                throw new KeyNotFoundException($"Booking with ID {request.BookingId} not found.");
            }
            if (booking.ClientId != request.ClientId)
            {
                logger.LogWarning("Booking confirmation failed. Client {ClientId} is not assigned to booking {BookingId}", request.ClientId, request.BookingId);
                throw new UnauthorizedAccessException($"Client with ID {request.ClientId} is not assigned to booking {request.BookingId}.");
            }
            if (booking.BookingStatus != BookingStatus.AwaitingClientStartConfirmation)
            {
                throw new InvalidOperationException($"Booking with ID {request.BookingId} must be requested to start before the client can confirm it.");
            }
            if (booking.DateOfService.Date != DateTime.UtcNow.Date)
            {
                throw new InvalidOperationException($"Booking with ID {request.BookingId} is not scheduled for today and cannot be confirmed as in progress.");
            }

            booking.StartJob();
            await repo.Bookings.UpdateBooking(booking, cancellationToken);
            await notificationService.JobStartedAsync(booking, cancellationToken);
            logger.LogInformation("Client {ClientId} confirmed job started for booking {BookingId}", request.ClientId, request.BookingId);
            return true;
        }
    }
}
