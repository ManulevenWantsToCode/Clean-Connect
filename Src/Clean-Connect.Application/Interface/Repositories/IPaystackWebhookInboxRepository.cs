using Clean_Connect.Domain.Entities;

namespace Clean_Connect.Application.Interface.Repositories;

public interface IPaystackWebhookInboxRepository
{
    Task<(PaystackWebhookInboxEvent Event, bool Inserted)> RecordAsync(PaystackWebhookInboxEvent webhookEvent, CancellationToken cancellationToken);
    Task<PaystackWebhookInboxEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> TryClaimAsync(Guid id, DateTime nowUtc, DateTime staleBeforeUtc, int maximumAttempts, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetRetryableIdsAsync(DateTime nowUtc, DateTime staleBeforeUtc, int maximumAttempts, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaystackWebhookInboxEvent>> GetQuarantinedAsync(int limit, CancellationToken cancellationToken);
    Task<bool> RequeueQuarantinedAsync(Guid id, string operatorId, DateTime nowUtc, CancellationToken cancellationToken);
    Task MarkProcessedAsync(Guid id, DateTime nowUtc, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid id, string safeErrorCategory, DateTime attemptedAtUtc, DateTime nextAttemptAtUtc, bool quarantine, CancellationToken cancellationToken);
}
