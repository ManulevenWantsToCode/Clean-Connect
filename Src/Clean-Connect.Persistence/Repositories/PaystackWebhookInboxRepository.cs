using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using Npgsql;

namespace Clean_Connect.Persistence.Repositories;

public sealed class PaystackWebhookInboxRepository(ApplicationDbContext db) : IPaystackWebhookInboxRepository
{
    public async Task<(PaystackWebhookInboxEvent Event, bool Inserted)> RecordAsync(PaystackWebhookInboxEvent webhookEvent, CancellationToken cancellationToken)
    {
        db.PaystackWebhookInboxEvents.Add(webhookEvent);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return (webhookEvent, true);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(webhookEvent).State = EntityState.Detached;
            var existing = await db.PaystackWebhookInboxEvents.SingleOrDefaultAsync(x => x.EventKey == webhookEvent.EventKey, cancellationToken);
            if (existing is null)
                throw;
            return (existing, false);
        }
    }

    public Task<PaystackWebhookInboxEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.PaystackWebhookInboxEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> TryClaimAsync(Guid id, DateTime nowUtc, DateTime staleBeforeUtc, int maximumAttempts, CancellationToken cancellationToken)
    {
        var affected = await db.PaystackWebhookInboxEvents
            .Where(x => x.Id == id && x.RetryCount < maximumAttempts
                && ((x.ProcessingStatus == "Received")
                    || (x.ProcessingStatus == "Failed" && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= nowUtc))
                    || (x.ProcessingStatus == "Processing" && x.ProcessingStartedAtUtc < staleBeforeUtc)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProcessingStatus, "Processing")
                .SetProperty(x => x.ProcessingStartedAtUtc, nowUtc)
                .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                .SetProperty(x => x.RetryCount, x => x.RetryCount + 1)
                .SetProperty(x => x.LastAttemptedAtUtc, nowUtc)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTime?)null)
                .SetProperty(x => x.LastErrorCategory, (string?)null), cancellationToken);
        return affected == 1;
    }

    public async Task<IReadOnlyList<Guid>> GetRetryableIdsAsync(DateTime nowUtc, DateTime staleBeforeUtc, int maximumAttempts, int limit, CancellationToken cancellationToken)
    {
        await db.PaystackWebhookInboxEvents
            .Where(x => x.ProcessingStatus == "Processing" && x.ProcessingStartedAtUtc < staleBeforeUtc && x.RetryCount >= maximumAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProcessingStatus, "Quarantined")
                .SetProperty(x => x.ProcessingStartedAtUtc, (DateTime?)null)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTime?)null)
                .SetProperty(x => x.QuarantinedAtUtc, nowUtc)
                .SetProperty(x => x.LastErrorCategory, "ProcessingInterruptedMaxAttempts"), cancellationToken);

        return await db.PaystackWebhookInboxEvents.AsNoTracking()
            .Where(x => x.RetryCount < maximumAttempts
                && (x.ProcessingStatus == "Received"
                    || (x.ProcessingStatus == "Failed" && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= nowUtc))
                    || (x.ProcessingStatus == "Processing" && x.ProcessingStartedAtUtc < staleBeforeUtc)))
            .OrderBy(x => x.ReceivedAtUtc)
            .Select(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaystackWebhookInboxEvent>> GetQuarantinedAsync(int limit, CancellationToken cancellationToken) =>
        await db.PaystackWebhookInboxEvents.AsNoTracking()
            .Where(x => x.ProcessingStatus == "Quarantined")
            .OrderBy(x => x.QuarantinedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken)
            ;

    public async Task<bool> RequeueQuarantinedAsync(Guid id, string operatorId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var affected = await db.PaystackWebhookInboxEvents
            .Where(x => x.Id == id && x.ProcessingStatus == "Quarantined")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProcessingStatus, "Failed")
                .SetProperty(x => x.RetryCount, 0)
                .SetProperty(x => x.NextAttemptAtUtc, nowUtc)
                .SetProperty(x => x.QuarantinedAtUtc, (DateTime?)null)
                .SetProperty(x => x.LastRequeuedBy, operatorId)
                .SetProperty(x => x.RequeueCount, x => x.RequeueCount + 1), cancellationToken);
        return affected == 1;
    }

    public async Task MarkProcessedAsync(Guid id, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await db.PaystackWebhookInboxEvents.Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProcessingStatus, "Processed")
                .SetProperty(x => x.ProcessedAtUtc, nowUtc)
                .SetProperty(x => x.ProcessingStartedAtUtc, (DateTime?)null)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTime?)null)
                .SetProperty(x => x.QuarantinedAtUtc, (DateTime?)null)
                .SetProperty(x => x.LastErrorCategory, (string?)null), cancellationToken);
    }

    public async Task MarkFailedAsync(Guid id, string safeErrorCategory, DateTime attemptedAtUtc, DateTime nextAttemptAtUtc, bool quarantine, CancellationToken cancellationToken)
    {
        var category = string.IsNullOrWhiteSpace(safeErrorCategory) ? "Unknown" : safeErrorCategory[..Math.Min(64, safeErrorCategory.Length)];
        await db.PaystackWebhookInboxEvents.Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ProcessingStatus, quarantine ? "Quarantined" : "Failed")
                .SetProperty(x => x.ProcessingStartedAtUtc, (DateTime?)null)
                .SetProperty(x => x.LastAttemptedAtUtc, attemptedAtUtc)
                .SetProperty(x => x.NextAttemptAtUtc, quarantine ? (DateTime?)null : nextAttemptAtUtc)
                .SetProperty(x => x.QuarantinedAtUtc, quarantine ? attemptedAtUtc : (DateTime?)null)
                .SetProperty(x => x.LastErrorCategory, category), cancellationToken);
    }
}
