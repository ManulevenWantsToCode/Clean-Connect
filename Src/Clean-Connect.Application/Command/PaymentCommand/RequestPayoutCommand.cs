using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.PaymentCommand
{
    public record RequestPayoutCommand(
        Guid BookingId,
        Guid WorkerId,
        string? ModifiedBy = null) : IRequest<PayoutResult>;

    public class RequestPayoutCommandValidator : AbstractValidator<RequestPayoutCommand>
    {
        public RequestPayoutCommandValidator()
        {
            RuleFor(x => x.BookingId)
                .NotEmpty()
                .WithMessage("BookingId is required.");

            RuleFor(x => x.WorkerId)
                .NotEmpty()
                .WithMessage("WorkerId is required.");
        }
    }

    public class RequestPayoutCommandHandler : IRequestHandler<RequestPayoutCommand, PayoutResult>
    {
        private readonly IUnitOfWork _repo;
        private readonly PayoutService _payoutService;
        private readonly ILogger<RequestPayoutCommandHandler> _logger;

        public RequestPayoutCommandHandler(
            IUnitOfWork repo,
            PayoutService payoutService,
            ILogger<RequestPayoutCommandHandler> logger)
        {
            _repo = repo;
            _payoutService = payoutService;
            _logger = logger;
        }

        public async Task<PayoutResult> Handle(RequestPayoutCommand request, CancellationToken cancellationToken)
        {
            var booking = await _repo.Bookings.GetBookingById(request.BookingId, cancellationToken);
            if (booking == null)
            {
                _logger.LogWarning("Payout failed. Booking not found: {BookingId}", request.BookingId);
                throw new KeyNotFoundException($"Booking with ID {request.BookingId} not found.");
            }

            if (booking.WorkerId != request.WorkerId)
            {
                _logger.LogWarning(
                    "Payout failed. Worker {WorkerId} is not assigned to booking {BookingId}",
                    request.WorkerId, request.BookingId);
                throw new UnauthorizedAccessException(
                    $"Worker with ID {request.WorkerId} is not assigned to booking {request.BookingId}.");
            }

            _logger.LogInformation(
                "Delegating payout to PayoutService for booking {BookingId} using stored bank details.",
                request.BookingId);

            return await _payoutService.PayoutAsync(booking, request.ModifiedBy, cancellationToken);
        }
    }
}