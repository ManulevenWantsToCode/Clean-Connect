using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
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
    public record AcceptBookingCommand(Guid BookingId, Guid WorkerId) : IRequest<bool>;

    public class AcceptBookingValidator : AbstractValidator<AcceptBookingCommand>
    {
        public AcceptBookingValidator()
        {
            RuleFor(x => x.BookingId)
            .NotEmpty()
            .WithMessage("BookingId is required.");
            RuleFor(x => x.WorkerId)
            .NotEmpty()
            .Must(id => id != Guid.Empty);
        }


    }

    public class AcceptBookingHandler : IRequestHandler<AcceptBookingCommand, bool>
    {
        private readonly AcceptBookingService _acceptBookingService;
        private readonly IUnitOfWork _repo;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AcceptBookingHandler> _logger;
        public AcceptBookingHandler(AcceptBookingService acceptBookingService, IUnitOfWork repo, INotificationService notificationService, ILogger<AcceptBookingHandler> logger)
        {
            _acceptBookingService = acceptBookingService;
            _repo = repo;
            _notificationService = notificationService;
            _logger = logger;
        }
        public async Task<bool> Handle(AcceptBookingCommand request, CancellationToken cancellationToken)
        {
            var booking = await _repo.Bookings.GetBookingById(request.BookingId, cancellationToken)
                ?? throw new KeyNotFoundException($"Booking with ID {request.BookingId} not found.");

            await _acceptBookingService.AcceptBookingAsync(request.BookingId, request.WorkerId, cancellationToken);
           

            booking.Accept();
            _logger.LogInformation("Worker {WorkerId} accepted  booking now awaiting payment{BookingId}", request.WorkerId, request.BookingId);

            await _repo.SaveChangesAsync(cancellationToken);
            await _notificationService.BookingAcceptedAsync(booking, cancellationToken);

            _logger.LogInformation("Booking {BookingId} accepted by worker now awaiting payment {WorkerId}", request.BookingId, request.WorkerId);


            return true;
        }
    }
}
