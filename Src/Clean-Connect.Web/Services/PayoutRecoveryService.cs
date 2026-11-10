using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Enums;

namespace Clean_Connect.Web.Services
{
    public sealed class PayoutRecoveryService(
        IServiceScopeFactory scopeFactory,
        ILogger<PayoutRecoveryService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ReconcileAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Payout recovery sweep failed.");
                }

                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task ReconcileAsync(CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repositories = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var payoutService = scope.ServiceProvider.GetRequiredService<PayoutService>();
            var escrows = await repositories.Escrows.GetAllEscrowsAsync(cancellationToken);

            foreach (var workerId in escrows
                .Where(x => x.Status == EscrowStatus.Held || x.TransferStatus == PayoutTransferStatus.LegacyUnverified)
                .Select(x => x.WorkerId)
                .Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await payoutService.PayoutPendingForWorkerAsync(workerId, "PayoutRecoveryService", cancellationToken);
            }
        }
    }
}
