using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.Services
{
    public class EscrowService
    {
        private readonly IUnitOfWork repo;
        private readonly ILogger<EscrowService> logger;
        private readonly IConfiguration configuration;
        private readonly Clean_Connect.Application.Interface.Repositories.IPlatformSettingsRepository platformSettings;

        public EscrowService(IUnitOfWork repo, ILogger<EscrowService> logger, IConfiguration configuration, Clean_Connect.Application.Interface.Repositories.IPlatformSettingsRepository platformSettings)
        {
            this.repo = repo;
            this.logger = logger;
            this.configuration = configuration;
            this.platformSettings = platformSettings;
        }

        public async Task HoldPaymentInEscrowAsync(Booking booking, Payment payment, CancellationToken cancellationToken)
        {
            if (payment.Status != PaymentStatus.Successful)
                throw new InvalidOperationException("Only successful payments can be held in escrow.");

            if (booking.PaymentStatus != PaymentStatus.Successful)
                throw new InvalidOperationException("Booking payment must be successful before escrow can be created.");

            var existingEscrow = await repo.Escrows.GetByBookingId(booking.Id, cancellationToken);
            if (existingEscrow != null)
            {
                logger.LogInformation("Escrow already exists for booking {BookingId}", booking.Id);
                return;
            }

            var settings = await platformSettings.GetAsync(cancellationToken);
            var commissionRate = settings.CommissionPercent;
            var commissionAmount = Math.Round(booking.OriginalAmount * (commissionRate / 100m), 2);

            var escrow = Escrow.Create(
                booking.Id,
                payment.Id,
                booking.WorkerId,
                booking.OriginalAmount,
                commissionRate,
                commissionAmount);

            await repo.Escrows.CreateEscrow(escrow, cancellationToken);

            logger.LogInformation(
                "Held {Amount} in escrow for booking {BookingId} and worker {WorkerId}",
                payment.Amount,
                booking.Id,
                booking.WorkerId);
        }
    }
}
