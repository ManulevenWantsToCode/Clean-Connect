using Clean_Connect.Domain.Utilities;

namespace Clean_Connect.Domain.Entities;

public sealed class PaystackWebhookInboxEvent : BaseEntity
{
    private PaystackWebhookInboxEvent() { }

    public string EventKey { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string? ProviderEventId { get; private set; }
    public string? PaymentReference { get; private set; }
    public string SafePayload { get; private set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; private set; }
    public string ProcessingStatus { get; private set; } = "Received";
    public int Attempts { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? ProcessingStartedAtUtc { get; private set; }
    public DateTime? LastAttemptedAtUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public DateTime? QuarantinedAtUtc { get; private set; }
    public string? LastErrorCategory { get; private set; }
    public int RequeueCount { get; private set; }
    public string? LastRequeuedBy { get; private set; }
    public string? CorrelationId { get; private set; }

    public static PaystackWebhookInboxEvent Receive(string eventKey, string eventType, string? providerEventId,
        string? paymentReference, string safePayload, DateTime receivedAtUtc, string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(eventKey) || eventKey.Length > 64)
            throw new ArgumentException("A valid event key is required.", nameof(eventKey));
        if (string.IsNullOrWhiteSpace(eventType) || eventType.Length > 100)
            throw new ArgumentException("A valid event type is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(safePayload))
            throw new ArgumentException("A sanitized event payload is required.", nameof(safePayload));

        return new PaystackWebhookInboxEvent
        {
            EventKey = eventKey,
            EventType = eventType,
            ProviderEventId = providerEventId,
            PaymentReference = paymentReference,
            SafePayload = safePayload,
            CorrelationId = IsSafeCorrelationId(correlationId) ? correlationId : null,
            ReceivedAtUtc = receivedAtUtc,
            ProcessingStatus = "Received"
        };
    }

    private static bool IsSafeCorrelationId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

}
