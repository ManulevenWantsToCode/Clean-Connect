using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Command.PaymentCommand;
using Clean_Connect.Application.Configuration;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clean_Connect.Application.Command.WebhookCommand;

public sealed record ProcessPaystackWebhookCommand(byte[] RawPayload, string Signature, string? CorrelationId = null) : IRequest<Unit>;
public sealed record ProcessStoredPaystackWebhookCommand(Guid InboxEventId) : IRequest<Unit>;

public sealed class ProcessPaystackWebhookCommandHandler :
    IRequestHandler<ProcessPaystackWebhookCommand, Unit>,
    IRequestHandler<ProcessStoredPaystackWebhookCommand, Unit>
{
    private static readonly JsonSerializerOptions WebhookJsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
    private readonly ILogger<ProcessPaystackWebhookCommandHandler> logger;
    private readonly IPaystackService paystackService;
    private readonly EscrowService escrowService;
    private readonly INotificationService notificationService;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITransactionManager transactionManager;
    private readonly IPaystackWebhookInboxRepository inboxRepository;
    private readonly IConfiguration configuration;
    private readonly PaystackWebhookRecoveryOptions recoveryOptions;

    public ProcessPaystackWebhookCommandHandler(
        ILogger<ProcessPaystackWebhookCommandHandler> logger,
        IPaystackService paystackService,
        EscrowService escrowService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IPaystackWebhookInboxRepository inboxRepository,
        IConfiguration configuration,
        IOptions<PaystackWebhookRecoveryOptions> recoveryOptions)
    {
        this.logger = logger;
        this.paystackService = paystackService;
        this.escrowService = escrowService;
        this.notificationService = notificationService;
        this.unitOfWork = unitOfWork;
        this.transactionManager = transactionManager;
        this.inboxRepository = inboxRepository;
        this.configuration = configuration;
        this.recoveryOptions = recoveryOptions.Value;
        this.recoveryOptions.Validate();
    }

    public async Task<Unit> Handle(ProcessPaystackWebhookCommand request, CancellationToken cancellationToken)
    {
        var secret = configuration["Paystack:WebhookSecret"] ?? configuration["Paystack:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret) || !IsValidSignature(request.RawPayload, request.Signature, secret))
            throw new UnauthorizedAccessException("Invalid signature.");

        var payload = JsonSerializer.Deserialize<PaystackWebhookDto>(request.RawPayload, WebhookJsonOptions)
            ?? throw new InvalidOperationException("Paystack webhook payload was empty or invalid.");
        if (string.IsNullOrWhiteSpace(payload.Event))
            throw new InvalidOperationException("Paystack webhook event type is missing.");

        var safePayload = SerializeSafePayload(payload);
        var providerEventId = payload.Data?.Id?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var eventKey = CreateEventKey(payload, providerEventId);
        var received = PaystackWebhookInboxEvent.Receive(
            eventKey,
            payload.Event,
            providerEventId,
            payload.Data?.Reference,
            safePayload,
            DateTime.UtcNow,
            request.CorrelationId);

        // Save the verified, sanitized event before any payment or escrow side effect.
        var (storedEvent, _) = await inboxRepository.RecordAsync(received, cancellationToken);
        await ProcessStoredAsync(storedEvent.Id, cancellationToken);
        return Unit.Value;
    }

    public Task<Unit> Handle(ProcessStoredPaystackWebhookCommand request, CancellationToken cancellationToken) =>
        ProcessStoredAsync(request.InboxEventId, cancellationToken);

    private async Task<Unit> ProcessStoredAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (!await inboxRepository.TryClaimAsync(eventId, now, now - TimeSpan.FromSeconds(recoveryOptions.ProcessingLeaseSeconds), recoveryOptions.MaximumAttempts, cancellationToken))
            return Unit.Value;

        string? storedCorrelationId = null;
        try
        {
            var stored = await inboxRepository.GetByIdAsync(eventId, cancellationToken)
                ?? throw new InvalidOperationException("Persisted webhook event was not found.");
            storedCorrelationId = stored.CorrelationId;
            using var correlationScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = stored.CorrelationId ?? eventId.ToString("N"),
                ["BackgroundOperation"] = "PaystackWebhook"
            });
            var payload = JsonSerializer.Deserialize<PaystackWebhookDto>(stored.SafePayload, WebhookJsonOptions)
                ?? throw new InvalidOperationException("Persisted webhook event was invalid.");

            // Provider verification is an external call and intentionally occurs before the DB transaction.
            PaystackVerifyResponse? verification = null;
            if (payload.Event is "charge.success" or "charge.failed")
            {
                var reference = payload.Data?.Reference;
                if (string.IsNullOrWhiteSpace(reference))
                    throw new InvalidOperationException("Charge event has no payment reference.");
                verification = await paystackService.VerifyTransaction(reference);
            }

            await transactionManager.ExecuteAsync(async ct =>
            {
                await ApplyEventAsync(payload, verification, ct);
                await inboxRepository.MarkProcessedAsync(eventId, DateTime.UtcNow, ct);
            }, cancellationToken);
            return Unit.Value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RecordFailureAsync(eventId, "ProcessingInterrupted", CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            var category = SafeErrorCategory(exception);
            await RecordFailureAsync(eventId, category, cancellationToken);
            using var correlationScope = logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = storedCorrelationId ?? eventId.ToString("N"),
                ["BackgroundOperation"] = "PaystackWebhook"
            });
            logger.LogError("Paystack webhook event {InboxEventId} failed with safe category {ErrorCategory}.", eventId, category);
            throw;
        }
    }

    private async Task RecordFailureAsync(Guid eventId, string safeCategory, CancellationToken cancellationToken)
    {
        var attempted = DateTime.UtcNow;
        var stored = await inboxRepository.GetByIdAsync(eventId, cancellationToken);
        if (stored is null) return;
        var quarantine = stored.Attempts >= recoveryOptions.MaximumAttempts;
        var nextAttempt = attempted + recoveryOptions.RetryDelay(Math.Max(1, stored.RetryCount));
        await inboxRepository.MarkFailedAsync(eventId, safeCategory, attempted, nextAttempt, quarantine, cancellationToken);
    }

    private static string SafeErrorCategory(Exception exception) => exception switch
    {
        OperationCanceledException => "ProcessingInterrupted",
        HttpRequestException or TimeoutException => "ProviderUnavailable",
        System.Net.Sockets.SocketException => "ProviderUnavailable",
        Microsoft.EntityFrameworkCore.DbUpdateException => "PersistenceFailure",
        InvalidOperationException or ArgumentException => "EventRejected",
        _ => "UnexpectedFailure"
    };

    private async Task ApplyEventAsync(PaystackWebhookDto payload, PaystackVerifyResponse? verification, CancellationToken cancellationToken)
    {
        if (payload.Event == "charge.success")
        {
            await ApplyChargeSuccessAsync(payload.Data!, verification!, cancellationToken);
            return;
        }
        if (payload.Event == "charge.failed")
        {
            await ApplyChargeFailureAsync(payload.Data!, verification!, cancellationToken);
            return;
        }
        if (payload.Event is "transfer.success" or "transfer.failed" or "transfer.reversed")
        {
            await ProcessTransferWebhookAsync(payload, cancellationToken);
            return;
        }
        if (payload.Event.StartsWith("refund.", StringComparison.Ordinal))
        {
            await ProcessRefundWebhookAsync(payload, cancellationToken);
        }
        // Unsupported event types are durably recorded and acknowledged as processed.
    }

    private async Task ApplyChargeSuccessAsync(PaystackWebhookData data, PaystackVerifyResponse verification, CancellationToken cancellationToken)
    {
        var reference = data.Reference;
        if (string.IsNullOrWhiteSpace(reference))
            throw new InvalidOperationException("Charge event has no payment reference.");
        var payment = await unitOfWork.Payments.GetFreshByReferenceAsync(reference, cancellationToken);
        if (payment is null)
            throw new InvalidOperationException("Payment reference does not match a local payment.");
        if (payment.Status == PaymentStatus.Successful)
            return;
        if (!verification.Status)
            throw new InvalidOperationException("Provider verification did not confirm the charge.");
        var verified = verification.Data
            ?? throw new InvalidOperationException("Provider verification did not confirm the charge.");
        if (!string.Equals(verified.Status, "success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Provider verification did not confirm the charge.");
        PaymentVerification.EnsureMatches(payment, verified);
        if (payment.Status != PaymentStatus.Pending)
            return;

        var booking = await unitOfWork.Bookings.GetBookingById(payment.BookingId, cancellationToken);
        if (booking is null || booking.Amount != payment.Amount)
            throw new InvalidOperationException("Booking does not match the verified payment.");

        payment.MarkAsPaid(verified.Authorization?.AuthorizationCode ?? string.Empty, verified.Id?.ToString() ?? reference);
        booking.MarkAsPaid();
        await escrowService.HoldPaymentInEscrowAsync(booking, payment, cancellationToken);
        await unitOfWork.Payments.UpdatePayment(payment, cancellationToken);
        await unitOfWork.Bookings.UpdateBooking(booking, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notificationService.PaymentConfirmedAsync(booking, cancellationToken);
    }

    private async Task ApplyChargeFailureAsync(PaystackWebhookData data, PaystackVerifyResponse verification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(data.Reference))
            throw new InvalidOperationException("Charge event has no payment reference.");
        var payment = await unitOfWork.Payments.GetFreshByReferenceAsync(data.Reference, cancellationToken);
        if (payment is null)
            throw new InvalidOperationException("Payment reference does not match a local payment.");
        if (payment.Status == PaymentStatus.Successful)
            return;
        if (!verification.Status)
            throw new InvalidOperationException("Verified failed charge does not match its local payment.");
        var verified = verification.Data
            ?? throw new InvalidOperationException("Verified failed charge does not match its local payment.");
        PaymentVerification.EnsureMatches(payment, verified);

        if (verified.Status is "failed" or "abandoned" or "reversed")
        {
            if (payment.Status == PaymentStatus.Pending)
            {
                payment.MarkAsFailed(verified.GatewayResponse, verified.Id?.ToString());
                await unitOfWork.Payments.UpdatePayment(payment, cancellationToken);
            }
            return;
        }

        // A failed webhook can arrive before the provider's final transaction state.
        // Leave the payment pending so a later verification/webhook can settle it.
    }

    private async Task ProcessTransferWebhookAsync(PaystackWebhookDto payload, CancellationToken cancellationToken)
    {
        var data = payload.Data;
        if (data is null || string.IsNullOrWhiteSpace(data.Reference))
            throw new InvalidOperationException("Transfer event has no reference.");

        var escrow = await unitOfWork.Escrows.GetByTransferReference(data.Reference, cancellationToken);
        if (escrow is null)
        {
            logger.LogWarning("Verified transfer event did not match a payout attempt.");
            return;
        }

        var expectedMinorUnits = decimal.Round(escrow.WorkerShare * 100m, 0, MidpointRounding.AwayFromZero);
        if (data.Amount != expectedMinorUnits)
            throw new InvalidOperationException("Verified transfer amount did not match escrow.");
        var payment = await unitOfWork.Payments.GetPaymentById(escrow.PaymentId, cancellationToken);
        if (payment is null || (!string.IsNullOrWhiteSpace(data.Currency)
            && !string.Equals(data.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Verified transfer currency did not match escrow payment.");

        var status = payload.Event switch
        {
            "transfer.success" => "success",
            "transfer.reversed" => "reversed",
            _ => "failed"
        };
        if (status == "success")
            escrow.RecordPayoutTransfer(data.TransferCode ?? string.Empty, status, "PaystackWebhook");
        else
            escrow.MarkPayoutRetryable(status, "PaystackWebhook");
        await unitOfWork.Escrows.UpdateEscrow(escrow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessRefundWebhookAsync(PaystackWebhookDto payload, CancellationToken cancellationToken)
    {
        var data = payload.Data;
        if (data is null)
            throw new InvalidOperationException("Refund event has no reference.");
        var refundReference = data.RefundReference;
        var transactionReference = data.TransactionReference;
        var payment = !string.IsNullOrWhiteSpace(refundReference)
            ? await unitOfWork.Payments.GetByRefundReferenceAsync(refundReference, cancellationToken)
            : null;
        payment ??= !string.IsNullOrWhiteSpace(transactionReference)
            ? await unitOfWork.Payments.GetFreshByReferenceAsync(transactionReference, cancellationToken)
            : null;
        if (payment is null)
        {
            logger.LogWarning("Verified refund event did not match a local refund attempt.");
            return;
        }
        if ((payload.Event == "refund.processed" && string.IsNullOrWhiteSpace(refundReference))
            || (!string.IsNullOrWhiteSpace(refundReference) && payment.RefundReference != refundReference)
            || data.Amount != decimal.Round(payment.RefundAmount * 100m, 0, MidpointRounding.AwayFromZero)
            || (!string.IsNullOrWhiteSpace(data.Currency) && !string.Equals(data.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Verified refund amount or currency did not match the local payment.");

        if (payload.Event == "refund.processed")
        {
            var shouldNotify = payment.Status != PaymentStatus.Refunded;
            payment.MarkAsRefunded("Refund confirmed by Paystack.");
            var booking = await unitOfWork.Bookings.GetBookingById(payment.BookingId, cancellationToken);
            var escrow = await unitOfWork.Escrows.GetByBookingId(payment.BookingId, cancellationToken);
            if (booking is not null) booking.RefundPayment();
            if (escrow is not null && escrow.Status == EscrowStatus.Held) escrow.MarkAsRefunded();
            if (booking is not null) await unitOfWork.Bookings.UpdateBooking(booking, cancellationToken);
            if (escrow is not null) await unitOfWork.Escrows.UpdateEscrow(escrow, cancellationToken);
            await unitOfWork.Payments.UpdatePayment(payment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (shouldNotify && booking is not null)
                await notificationService.RefundProcessedAsync(booking, cancellationToken);
            return;
        }
        else if (payload.Event is "refund.pending" or "refund.processing")
        {
            payment.MarkRefundPending(payload.Event["refund.".Length..]);
        }
        else
        {
            payment.MarkRefundPending("NeedsAttention");
        }
        await unitOfWork.Payments.UpdatePayment(payment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string SerializeSafePayload(PaystackWebhookDto payload) => JsonSerializer.Serialize(new
    {
        @event = payload.Event,
        data = payload.Data is null ? null : new
        {
            payload.Data.Id,
            payload.Data.Reference,
            payload.Data.Amount,
            payload.Data.Currency,
            payload.Data.Status,
            payload.Data.TransferCode,
            payload.Data.RefundReference,
            payload.Data.TransactionReference
        }
    }, WebhookJsonOptions);

    private static string CreateEventKey(PaystackWebhookDto payload, string? providerEventId)
    {
        var identity = !string.IsNullOrWhiteSpace(providerEventId)
            ? $"{payload.Event}|{providerEventId}"
            : $"{payload.Event}|{payload.Data?.Reference}|{payload.Data?.Status}|{payload.Data?.Amount}|{payload.Data?.Currency}|{payload.Data?.RefundReference}|{payload.Data?.TransactionReference}|{payload.Data?.TransferCode}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static bool IsValidSignature(byte[] payload, string signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;
        try
        {
            var expected = Convert.FromHexString(ComputeHash(payload, secret));
            var supplied = Convert.FromHexString(signature);
            return CryptographicOperations.FixedTimeEquals(expected, supplied);
        }
        catch (FormatException) { return false; }
    }

    private static string ComputeHash(byte[] payload, string secret)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }
}
