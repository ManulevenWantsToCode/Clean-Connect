namespace Clean_Connect.Domain.Entities;

/// <summary>Immutable metadata about a security- or business-relevant state change.</summary>
public sealed class AuditRecord
{
    private AuditRecord() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; private set; }
    public string? ActorId { get; private set; }
    public string ActorKind { get; private set; } = "System";
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string ChangedFields { get; private set; } = "[]";
    public string? CorrelationId { get; private set; }

    public static AuditRecord Create(DateTime occurredAtUtc, string? actorId, string action,
        string entityType, string entityId, string changedFields, string? correlationId) => new()
    {
        OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc),
        ActorId = string.IsNullOrWhiteSpace(actorId) ? null : actorId[..Math.Min(actorId.Length, 100)],
        ActorKind = string.IsNullOrWhiteSpace(actorId) ? "System" : "User",
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        ChangedFields = changedFields,
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId[..Math.Min(correlationId.Length, 64)]
    };

    public static AuditRecord CreateAuthenticationEvent(DateTime occurredAtUtc, Guid? accountId,
        bool succeeded, string? correlationId)
    {
        var record = Create(occurredAtUtc, accountId?.ToString(),
            succeeded ? "LoginSucceeded" : "LoginFailed", "Authentication", Guid.NewGuid().ToString("N"), "[]", correlationId);
        if (accountId is null) record.ActorKind = "Anonymous";
        return record;
    }
}
