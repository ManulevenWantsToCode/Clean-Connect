using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.BookingCommand
{
    public record MarkExpiredBookingsCommand : IRequest<int>;

    public class MarkExpiredBookingsHandler(
        IUnitOfWork repo,
        INotificationService notificationService,
        RefundService refundService,
        ILogger<MarkExpiredBookingsHandler> logger) : IRequestHandler<MarkExpiredBookingsCommand, int>
    {
        public async Task<int> Handle(MarkExpiredBookingsCommand request, CancellationToken cancellationToken)
        {
            var bookings = await repo.Bookings.GetExpiredBookingsAsync(cancellationToken);

            var newlyExpired = new List<Booking>();

            foreach (var booking in bookings)
            {
                if (booking.CanExpire)
                {
                    booking.MarkAsExpired();
                    await repo.Bookings.UpdateBooking(booking, cancellationToken);
                    newlyExpired.Add(booking);
                }

                if (booking.PaymentStatus == PaymentStatus.Successful)
                {
                    var refunded = await refundService.TryRefundExpiredBookingAsync(booking, cancellationToken);
                    if (refunded)
                    {
                        await notificationService.RefundProcessedAsync(booking, cancellationToken);
                    }
                }
            }

            await repo.SaveChangesAsync(cancellationToken);

            foreach (var booking in newlyExpired)
            {
                await notificationService.BookingExpiredAsync(booking, cancellationToken);
            }

            logger.LogInformation("Marked {Count} bookings as expired.", newlyExpired.Count);
            return newlyExpired.Count;
        }
    }
}