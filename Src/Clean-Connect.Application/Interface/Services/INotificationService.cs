using Clean_Connect.Domain.Entities;

namespace Clean_Connect.Application.Interface.Services
{
    public interface INotificationService
    {
        Task NewBookingRequestAsync(Booking booking, CancellationToken cancellationToken);
        Task BookingAcceptedAsync(Booking booking, CancellationToken cancellationToken);
        Task BookingRejectedAsync(Booking booking, CancellationToken cancellationToken);
        Task StartJobRequestedAsync(Booking booking, CancellationToken cancellationToken);
        Task JobStartedAsync(Booking booking, CancellationToken cancellationToken);
        Task JobFinishedByWorkerAsync(Booking booking, CancellationToken cancellationToken);
        Task JobCompletedAsync(Booking booking, CancellationToken cancellationToken);
        Task PaymentConfirmedAsync(Booking booking, CancellationToken cancellationToken);
        Task BookingExpiredAsync(Booking booking, CancellationToken cancellationToken);
        Task RefundProcessedAsync(Booking booking, CancellationToken cancellationToken);
        Task ReferralCouponAwardedAsync(Client referrer, string couponCode, CancellationToken cancellationToken);
    }
}