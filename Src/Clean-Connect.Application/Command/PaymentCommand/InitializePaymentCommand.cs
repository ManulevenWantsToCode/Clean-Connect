using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clean_Connect.Application.Command.PaymentCommand
{
    public record InitializePaymentCommand(Guid BookingId, decimal Amount, string Email, string PaymentMethod, string? TransactionId, string? CreatedBy) : IRequest<string>;

    public class InitializePaymentHandler : IRequestHandler<InitializePaymentCommand, string>
    {
        private readonly IUnitOfWork repo;
        private readonly IPaystackService paystackService;
        private readonly ILogger<InitializePaymentHandler> logger;


        public InitializePaymentHandler(IUnitOfWork _repo, IPaystackService _paystackService, ILogger<InitializePaymentHandler> _logger)
        {
            repo = _repo;
            paystackService = _paystackService;
            logger = _logger;
        }




        public class InitializePaymentCommandValidator : AbstractValidator<InitializePaymentCommand>
        {
            public InitializePaymentCommandValidator()
            {
                RuleFor(x => x.BookingId)
                    .NotEmpty()
                    .WithMessage("BookingId is required");

                RuleFor(x => x.Amount)
                    .GreaterThan(0)
                    .WithMessage("Amount must be greater than zero");

                RuleFor(x => x.Email)
                    .NotEmpty()
                    .EmailAddress()
                    .WithMessage("A valid email is required");

                RuleFor(x => x.PaymentMethod) // change to PaymentMethod after fixing
                    .NotEmpty()
                    .MaximumLength(50)
                    .WithMessage("Payment method is required");

                RuleFor(x => x.TransactionId)
                    .MaximumLength(100)
                    .When(x => !string.IsNullOrEmpty(x.TransactionId));

                RuleFor(x => x.CreatedBy)
                    .MaximumLength(100)
                    .When(x => !string.IsNullOrEmpty(x.CreatedBy));
            }
        }


        public async Task<string> Handle(InitializePaymentCommand request, CancellationToken cancellationToken)
        {

            var booking = await repo.Bookings.GetBookingById(request.BookingId, cancellationToken);
            if (booking == null)
            {
                logger.LogError("Booking not found for ID: {BookingId}", request.BookingId);
                throw new ArgumentNullException(nameof(booking));
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

            if (request.Amount != booking.Amount)
            {
                logger.LogWarning(
                    "Payment initialization amount did not match booking {BookingId}.",
                    request.BookingId);
                throw new InvalidOperationException("Payment amount must match the booking amount.");
            }

            // 1. Generate unique reference
            var reference = Guid.NewGuid().ToString();
            logger.LogInformation("Generated a payment reference for booking {BookingId}.", request.BookingId);

            // 2. Create payment (Pending)

            var payment = Payment.Create(request.BookingId, booking.Amount, reference, request.PaymentMethod, request.TransactionId ?? string.Empty, DateTime.UtcNow, request.CreatedBy);
            logger.LogInformation("Created a pending payment record for booking {BookingId}.", request.BookingId);

            await repo.Payments.CreatePayment(payment, cancellationToken);
            logger.LogInformation("Saved the pending payment record for booking {BookingId}.", request.BookingId);

            var response = await paystackService.InitializePayment(booking.Amount, request.Email, reference);
            logger.LogInformation("Paystack initialization completed for booking {BookingId} with status {Status}.", request.BookingId, response.Status);
            if (!response.Status)


                throw new ArgumentException("Payment initialization failed");

            logger.LogInformation("Payment initialization succeeded for booking {BookingId}.", request.BookingId);

            return response.AuthorizationUrl;

        }
    }

}
