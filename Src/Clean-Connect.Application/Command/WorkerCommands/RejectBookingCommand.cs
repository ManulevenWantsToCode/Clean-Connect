using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Command.WorkerCommands
{
    public record RejectBookingCommand(Guid BookingId, Guid WorkerId) : IRequest<bool>;

    public class RejectBookingValidator : AbstractValidator<RejectBookingCommand>
    {
        public RejectBookingValidator()
        {
            RuleFor(x => x.BookingId)
            .NotEmpty()
            .WithMessage("BookingId is required.");
            RuleFor(x => x.WorkerId)
            .NotEmpty()
            .Must(id => id != Guid.Empty);
        }

    }

    public class RejectBookingHandler : IRequestHandler<RejectBookingCommand, bool>
    {
        private readonly AcceptBookingService _acceptBookingService;
        private readonly IUnitOfWork _repo;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AcceptBookingHandler> _logger;
        public RejectBookingHandler(AcceptBookingService acceptBookingService, IUnitOfWork repo, INotificationService notificationService, ILogger<AcceptBookingHandler> logger)
        {
            _acceptBookingService = acceptBookingService;
            _repo = repo;
            _notificationService = notificationService;
            _logger = logger;
        }
        public async Task<bool> Handle(RejectBookingCommand request, CancellationToken cancellationToken)
        {
            var booking = await _repo.Bookings.GetBookingById(request.BookingId, cancellationToken)
                ?? throw new KeyNotFoundException($"Booking with ID {request.BookingId} not found.");

            await _acceptBookingService.AcceptBookingAsync(request.BookingId, request.WorkerId, cancellationToken);
            _logger.LogInformation("Worker {WorkerId} Rejected booking {BookingId}", request.WorkerId, request.BookingId);

            booking.Reject();
            await _repo.SaveChangesAsync(cancellationToken);
            await _notificationService.BookingRejectedAsync(booking, cancellationToken);

            _logger.LogInformation("Booking {BookingId} Rejected by worker {WorkerId}", request.BookingId, request.WorkerId);


            return true;
        }
    }
}