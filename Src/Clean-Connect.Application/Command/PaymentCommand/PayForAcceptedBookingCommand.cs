using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.PaymentCommand
{
    public record PayForAcceptedBookingCommand(
        Guid BookingId,
        Guid ClientId,
        string Email,
        string PaymentMethod,
        string? CreatedBy = null,
        string? CallbackUrl = null) : IRequest<PaymentInitializationResponse>;

    public class PayForAcceptedBookingCommandValidator : AbstractValidator<PayForAcceptedBookingCommand>
    {
        public PayForAcceptedBookingCommandValidator()
        {
            RuleFor(x => x.BookingId)
                .NotEmpty()
                .WithMessage("BookingId is required.");

            RuleFor(x => x.ClientId)
                .NotEmpty()
                .WithMessage("ClientId is required.");

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .WithMessage("A valid email is required.");

            RuleFor(x => x.PaymentMethod)
                .NotEmpty()
                .MaximumLength(50)
                .WithMessage("Payment method is required.");

            RuleFor(x => x.CreatedBy)
                .MaximumLength(100)
                .When(x => !string.IsNullOrWhiteSpace(x.CreatedBy));
        }
    }

    public class PayForAcceptedBookingHandler : IRequestHandler<PayForAcceptedBookingCommand, PaymentInitializationResponse>
    {
        private readonly IUnitOfWork repo;
        private readonly IPaystackService paystackService;
        private readonly ILogger<PayForAcceptedBookingHandler> logger;

        public PayForAcceptedBookingHandler(
            IUnitOfWork repo,
            IPaystackService paystackService,
            ILogger<PayForAcceptedBookingHandler> logger)
        {
            this.repo = repo;
            this.paystackService = paystackService;
            this.logger = logger;
        }

        public async Task<PaymentInitializationResponse> Handle(PayForAcceptedBookingCommand request, CancellationToken cancellationToken)
        {
            var booking = await repo.Bookings.GetBookingById(request.BookingId, cancellationToken);
            if (booking == null)
            {
                logger.LogWarning("Payment initialization failed. Booking not found: {BookingId}", request.BookingId);
                throw new KeyNotFoundException($"Booking with ID {request.BookingId} not found.");
            }

            if (booking.ClientId != request.ClientId)
            {
                logger.LogWarning(
                    "Payment initialization failed. Client {ClientId} is not assigned to booking {BookingId}",
                    request.ClientId,
                    request.BookingId);
                throw new UnauthorizedAccessException($"Client with ID {request.ClientId} is not assigned to booking {request.BookingId}.");
            }

            if (booking.BookingStatus != BookingStatus.AcceptedAwaitingPayment)
            {
                logger.LogWarning(
                    "Payment initialization failed. Booking {BookingId} status is {BookingStatus}",
                    request.BookingId,
                    booking.BookingStatus);
                throw new InvalidOperationException("Booking must be accepted by the worker before payment can be initialized.");
            }

            if (booking.PaymentStatus == PaymentStatus.Successful)
            {
                logger.LogInformation("Payment initialization skipped. Booking {BookingId} is already paid.", request.BookingId);
                throw new InvalidOperationException("Booking has already been paid.");
            }

            var reference = Guid.NewGuid().ToString();
            var payment = Payment.Create(
                request.BookingId,
                booking.Amount,
                reference,
                request.PaymentMethod,
                string.Empty,
                DateTime.UtcNow);

            await repo.Payments.CreatePayment(payment, cancellationToken);

            var response = await paystackService.InitializePayment(booking.Amount, request.Email, reference, request.CallbackUrl);
            if (!response.Status)
            {
                throw new InvalidOperationException("Payment initialization failed.");
            }

            logger.LogInformation(
                "Payment initialized for booking {BookingId} with reference {PaymentReference}",
                request.BookingId,
                reference);

            return new PaymentInitializationResponse(
                request.BookingId,
                booking.Amount,
                response.Reference,
                response.AuthorizationUrl);
        }
    }
}
