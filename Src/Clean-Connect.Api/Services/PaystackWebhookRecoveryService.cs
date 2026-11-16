using Clean_Connect.Application.Command.WebhookCommand;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Configuration;
using MediatR;
using Microsoft.Extensions.Options;
using Clean_Connect.Infrastructure.Operational;

namespace Clean_Connect.Api.Services;

public sealed class PaystackWebhookRecoveryService(
    IServiceScopeFactory scopeFactory,
    ILogger<PaystackWebhookRecoveryService> logger,
    IOptions<PaystackWebhookRecoveryOptions> options) : BackgroundService
{
    private readonly PaystackWebhookRecoveryOptions recoveryOptions = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        recoveryOptions.Validate();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(recoveryOptions.PollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            using var correlationScope = BackgroundCorrelationScope.Begin(logger, nameof(PaystackWebhookRecoveryService));
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("Webhook inbox recovery batch failed with {ExceptionType}.", exception.GetType().Name);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> ids;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IPaystackWebhookInboxRepository>();
            var now = DateTime.UtcNow;
            ids = await inbox.GetRetryableIdsAsync(now, now - TimeSpan.FromSeconds(recoveryOptions.ProcessingLeaseSeconds),
                recoveryOptions.MaximumAttempts, recoveryOptions.BatchSize, cancellationToken);
        }

        foreach (var id in ids)
        {
            // A fresh scoped DbContext isolates failed tracked changes from later work items.
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(new ProcessStoredPaystackWebhookCommand(id), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Webhook inbox event {InboxEventId} remains retryable after {ExceptionType}.", id, exception.GetType().Name);
            }
        }
    }
}
