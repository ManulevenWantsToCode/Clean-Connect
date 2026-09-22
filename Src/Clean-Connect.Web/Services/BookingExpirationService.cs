using Clean_Connect.Application.Command.BookingCommand;
using MediatR;

namespace Clean_Connect.Web.Services
{
    public class BookingExpirationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BookingExpirationService> _logger;
        private readonly TimeSpan _interval;

        public BookingExpirationService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<BookingExpirationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            var minutes = configuration.GetValue("BookingExpiration:CheckIntervalMinutes", 10);
            _interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_interval);

            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var count = await mediator.Send(new MarkExpiredBookingsCommand(), stoppingToken);
                    if (count > 0)
                    {
                        _logger.LogInformation("Booking expiration check marked {Count} bookings as expired.", count);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Booking expiration check failed.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
