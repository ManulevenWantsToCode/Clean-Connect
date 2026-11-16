using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Clean_Connect.Application.Command.PaymentCommand
{
    public record PayForAcceptedBookingCommand(
        Guid BookingId,
        Guid ClientId,
        string Email,
        string PaymentMethod,
        string? CreatedBy = null,
        string? CallbackUrl = null,
        string? IdempotencyKey = null) : IRequest<PaymentInitializationResponse>;

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

            RuleFor(x => x.IdempotencyKey)
                .NotEmpty()
                .MaximumLength(128)
                .WithMessage("A valid Idempotency-Key is required.");

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
        private readonly IConfiguration configuration;
        private readonly IMediator mediator;

        public PayForAcceptedBookingHandler(
            IUnitOfWork repo,
            IPaystackService paystackService,
            ILogger<PayForAcceptedBookingHandler> logger,
            IConfiguration configuration,
            IMediator mediator)
        {
            this.repo = repo;
            this.paystackService = paystackService;
            this.logger = logger;
            this.configuration = configuration;
            this.mediator = mediator;
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

            var currency = configuration["Paystack:Currency"]?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(currency)) currency = "NGN";
            if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
                throw new InvalidOperationException("Paystack currency configuration is invalid.");

            if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 128)
                throw new PaymentInitializationConflictException("A valid idempotency key is required.");

            var requestHash = PaymentIdempotency.ComputeRequestHash(request.BookingId, booking.Amount, request.Email, request.PaymentMethod, currency);
            var existing = await repo.Payments.GetByBookingAndIdempotencyKeyAsync(request.BookingId, request.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                PaymentIdempotency.EnsureSameRequest(existing, requestHash);
                return await ResolveExistingAttemptAsync(existing, cancellationToken);
            }

            var activeAttempt = await repo.Payments.GetPendingByBookingAsync(request.BookingId, cancellationToken);
            if (activeAttempt is not null)
            {
                if (activeAttempt.IdempotencyKey != request.IdempotencyKey)
                    throw new PaymentInitializationConflictException("A payment attempt for this booking is still unresolved.");
                PaymentIdempotency.EnsureSameRequest(activeAttempt, requestHash);
                return await ResolveExistingAttemptAsync(activeAttempt, cancellationToken);
            }

            var reference = $"cc-{Guid.NewGuid():N}";
            var payment = Payment.Create(
                request.BookingId,
                booking.Amount,
                reference,
                request.PaymentMethod,
                string.Empty,
                DateTime.UtcNow,
                request.CreatedBy,
                request.IdempotencyKey,
                requestHash,
                currency);

            var reservation = await repo.Payments.CreatePaymentIdempotentlyAsync(payment, cancellationToken);
            if (!reservation.Created)
            {
                PaymentIdempotency.EnsureSameRequest(reservation.Payment, requestHash);
                if (reservation.Payment.IdempotencyKey != request.IdempotencyKey)
                    throw new PaymentInitializationConflictException("A payment attempt for this booking is still unresolved.");
                return await ResolveExistingAttemptAsync(reservation.Payment, cancellationToken);
            }

            // The local attempt and unique reference are committed before the provider call.
            // Paystack uses the supplied unique transaction reference; it does not document
            // a separate idempotency header for transaction initialization.
            payment.MarkInitializationAttempted(DateTime.UtcNow);
            await repo.Payments.UpdatePayment(payment, cancellationToken);

            PaystackInitResponse response;
            string providerReference;
            string authorizationUrl;
            try
            {
                response = await paystackService.InitializePayment(booking.Amount, request.Email, reference, request.CallbackUrl, currency);
                var returnedReference = response.Reference;
                var returnedAuthorizationUrl = response.AuthorizationUrl;
                if (!response.Status || string.IsNullOrWhiteSpace(returnedReference)
                    || !string.Equals(returnedReference, reference, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(returnedAuthorizationUrl))
                    throw new InvalidOperationException("Paystack initialization response was incomplete.");

                providerReference = returnedReference;
                authorizationUrl = returnedAuthorizationUrl;
                payment.SetAuthorizationUrl(authorizationUrl);
                await repo.Payments.UpdatePayment(payment, cancellationToken);
            }
            catch (PaystackInitializationRejectedException)
            {
                payment.MarkInitializationFailed("ProviderRejected");
                payment.MarkAsFailed("ProviderRejected", null);
                await repo.Payments.UpdatePayment(payment, CancellationToken.None);
                throw new PaymentInitializationConflictException("Paystack rejected this attempt. Start a new attempt with a new idempotency key.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await MarkReconciliationBestEffortAsync(payment, "ProviderOutcomeUnknown");
                throw;
            }
            catch (Exception exception)
            {
                var category = exception is HttpRequestException or TimeoutException ? "ProviderOutcomeUnknown" : "InitializationPersistenceUncertain";
                await MarkReconciliationBestEffortAsync(payment, category);
                throw new PaymentInitializationConflictException("The payment provider outcome is being reconciled. Do not start another payment attempt.");
            }

            logger.LogInformation(
                "Payment initialized for booking {BookingId}.",
                request.BookingId);

            return new PaymentInitializationResponse(
                request.BookingId,
                booking.Amount,
                providerReference,
                authorizationUrl);
        }

        private async Task<PaymentInitializationResponse> ResolveExistingAttemptAsync(Payment payment, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(payment.AuthorizationUrl))
                return ExistingResult(payment);
            if (payment.Status != PaymentStatus.Pending || payment.InitializationStatus == PaymentInitializationStatus.Failed)
                throw new PaymentInitializationConflictException("This payment attempt failed. Use a new idempotency key to start another attempt.");

            try
            {
                var verification = await paystackService.VerifyTransaction(payment.PaymentReference);
                if (!verification.Status)
                    throw new InvalidOperationException("Provider could not confirm the initialization outcome.");
                var verified = verification.Data
                    ?? throw new InvalidOperationException("Provider could not confirm the initialization outcome.");
                PaymentVerification.EnsureMatches(payment, verified);

                if (string.Equals(verified.Status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    await mediator.Send(new ConfirmPaymentCommand(payment.PaymentReference), cancellationToken);
                    throw new PaymentInitializationConflictException("The provider confirms this payment is complete. Refresh the booking status.");
                }

                if (verified.Status is "failed" or "abandoned" or "reversed")
                {
                    payment.MarkInitializationFailed("ProviderTransactionFailed");
                    payment.MarkAsFailed("ProviderTransactionFailed", verified.Id?.ToString());
                    await repo.Payments.UpdatePayment(payment, cancellationToken);
                    throw new PaymentInitializationConflictException("The provider confirms this attempt failed. Use a new idempotency key to retry.");
                }

                payment.RequireInitializationReconciliation("ProviderTransactionPending");
                await repo.Payments.UpdatePayment(payment, cancellationToken);
                throw new PaymentInitializationConflictException("The provider has the transaction, but checkout details are unavailable. This attempt requires reconciliation.");
            }
            catch (PaystackTransactionNotFoundException)
            {
                // A verification 404 only establishes that the reference is not visible now.
                // After a timed-out initialization, treat it as unresolved rather than closing
                // the attempt and allowing a fresh reference that could create a second charge.
                payment.RequireInitializationReconciliation("ProviderReferenceNotFound");
                await repo.Payments.UpdatePayment(payment, cancellationToken);
                throw new PaymentInitializationConflictException("Paystack has not confirmed this attempt. It remains in reconciliation; do not start another payment attempt.");
            }
            catch (PaymentInitializationConflictException)
            {
                throw;
            }
            catch (Exception exception)
            {
                var category = exception is HttpRequestException or TimeoutException ? "ProviderVerificationUnavailable" : "ProviderVerificationMismatch";
                payment.RequireInitializationReconciliation(category);
                await repo.Payments.UpdatePayment(payment, CancellationToken.None);
                logger.LogWarning("Payment initialization {PaymentId} remains unresolved ({ErrorCategory}).", payment.Id, category);
                throw new PaymentInitializationConflictException("The payment provider outcome is being reconciled. Do not start another payment attempt.");
            }
        }

        private async Task MarkReconciliationBestEffortAsync(Payment payment, string category)
        {
            payment.RequireInitializationReconciliation(category);
            try
            {
                await repo.Payments.UpdatePayment(payment, CancellationToken.None);
            }
            catch (Exception persistenceException)
            {
                logger.LogError("Could not persist payment reconciliation state for {PaymentId} ({ExceptionType}).", payment.Id, persistenceException.GetType().Name);
            }
        }

        private static PaymentInitializationResponse ExistingResult(Payment payment)
        {
            return new PaymentInitializationResponse(payment.BookingId, payment.Amount, payment.PaymentReference, payment.AuthorizationUrl!);
        }
    }
}
