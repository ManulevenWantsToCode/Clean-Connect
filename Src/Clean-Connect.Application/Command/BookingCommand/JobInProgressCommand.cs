using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.BookingCommand
{
    public record RequestJobStartCommand(Guid WorkerId, Guid BookingId) : IRequest<bool>;

    public class RequestJobStartValidator : AbstractValidator<RequestJobStartCommand>
    {
        public RequestJobStartValidator()
        {
            RuleFor(x => x.WorkerId)
                .NotEmpty()
                .WithMessage("Worker Id is required")
                .Must(id => id != Guid.Empty)
                .WithMessage("Invalid Id");

            RuleFor(x => x.BookingId)
                .NotEmpty()
                .WithMessage("Booking Id is required")
                .Must(id => id != Guid.Empty)
                .WithMessage("Invalid Id");
        }
    }
    public class RequestJobStartHandler : IRequestHandler<RequestJobStartCommand, bool>
    {
        private readonly IUnitOfWork repo;
        private readonly INotificationService notificationService;
        private readonly ILogger<RequestJobStartHandler> logger;

        public RequestJobStartHandler(IUnitOfWork _repo, INotificationService notificationService, ILogger<RequestJobStartHandler> _logger)
        {
            repo = _repo;
            this.notificationService = notificationService;
            logger = _logger;
        }
        public async Task<bool> Handle(RequestJobStartCommand request, CancellationToken cancellationToken)
        {
            var booking = await repo.Bookings.GetBookingById(request.BookingId, cancellationToken);
            if (booking == null)
            {
                throw new KeyNotFoundException($"Booking with ID {request.BookingId} not found.");
            }
            if (booking.WorkerId != request.WorkerId)
            {
                throw new UnauthorizedAccessException($"Worker with ID {request.WorkerId} is not assigned to booking {request.BookingId}.");
            }
            if (booking.BookingStatus != BookingStatus.MarkAsPaid)
            {
                throw new InvalidOperationException($"Booking with ID {request.BookingId} is not in paid status and cannot be requested to start.");
            }

            if (booking.DateOfService.Date != DateTime.UtcNow.Date)
            {
                throw new InvalidOperationException($"Booking with ID {request.BookingId} is not scheduled for today and cannot be requested to start.");
            }

            booking.RequestStartJob();
            await repo.Bookings.UpdateBooking(booking, cancellationToken);
            await notificationService.StartJobRequestedAsync(booking, cancellationToken);
            return true;
        }

    }
}
