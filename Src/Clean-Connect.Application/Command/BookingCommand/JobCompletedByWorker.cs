using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.BookingCommand
{
    public record JobCompletedByWorkerCommand(Guid BookingId, Guid WorkerId) : IRequest<bool>;

    public class JobCompletedByWorkerCommandValidator : AbstractValidator<JobCompletedByWorkerCommand>
    {
        public JobCompletedByWorkerCommandValidator()
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

    public class JobCompletedByWorkerHandler : IRequestHandler<JobCompletedByWorkerCommand, bool>
    {
        private readonly IUnitOfWork repo;
        private readonly INotificationService notificationService;
        private readonly ILogger<JobCompletedByWorkerHandler> logger;

        public JobCompletedByWorkerHandler(IUnitOfWork repo, INotificationService notificationService, ILogger<JobCompletedByWorkerHandler> logger)
        {
            this.repo = repo;
            this.notificationService = notificationService;
            this.logger = logger;
        }

        public async Task<bool> Handle(JobCompletedByWorkerCommand request, CancellationToken cancellationToken)
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
            if (booking.BookingStatus != BookingStatus.InProgress)
            {
                throw new InvalidOperationException($"Booking with ID {request.BookingId} is not in in progress and cannot be marked as completed.");
            }

            if (booking.DateOfService.Date != DateTime.UtcNow.Date)
            {

                throw new InvalidOperationException($"Booking with ID {request.BookingId} is not scheduled for today and cannot be marked completed.");
            }
            booking.MarkAsAwaitingClientConfirmation();
            await repo.Bookings.UpdateBooking(booking, cancellationToken);
            await notificationService.JobFinishedByWorkerAsync(booking, cancellationToken);
            return true;
        }
    }

}