using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Command.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _repo;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IUnitOfWork repo, ILogger<NotificationService> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public async Task NewBookingRequestAsync(Booking booking, CancellationToken cancellationToken)
        {
            var notification = Notification.Create(
                targetId: booking.WorkerId,
                bookingId: booking.Id,
                audience: NotificationAudience.Worker,
                title: "New booking request",
                message: $"{booking.Client?.FullName?.ToString() ?? "A client"} requested {booking.ServiceType?.Name ?? "a cleaning service"} for {booking.DateOfService:MMM dd, yyyy}. Respond when you are ready.",
                status: booking.BookingStatus.ToString(),
                tone: "success",
                icon: "fa-inbox",
                actionText: "Respond Now",
                actionUrl: $"/Worker-Booking-Details/{booking.Id}",
                needsAttention: true,
                createdBy: booking.CreatedBy);
            await PersistAsync(notification, cancellationToken);
        }

        public async Task BookingAcceptedAsync(Booking booking, CancellationToken cancellationToken)
        {
            var notification = Notification.Create(
                targetId: booking.ClientId,
                bookingId: booking.Id,
                audience: NotificationAudience.Client,
                title: "Worker accepted your booking",
                message: $"{booking.Worker?.FullName?.ToString()} accepted your {booking.ServiceType?.Name ?? "cleaning service"} request. Continue to payment to secure the job.",
                status: booking.BookingStatus.ToString(),
                tone: "success",
                icon: "fa-circle-check",
                actionText: "Proceed to Payment",
                actionUrl: $"/Client-Booking-Details/{booking.Id}",
                needsAttention: true,
                createdBy: booking.CreatedBy);
            await PersistAsync(notification, cancellationToken);
        }

        public async Task BookingRejectedAsync(Booking booking, CancellationToken cancellationToken)
        {
            var notification = Notification.Create(
                targetId: booking.ClientId,
                bookingId: booking.Id,
                audience: NotificationAudience.Client,
                title: "Worker declined your booking",
                message: $"{booking.Worker?.FullName?.ToString()} declined your {booking.ServiceType?.Name ?? "cleaning service"} request. You can choose another worker.",
                status: booking.BookingStatus.ToString(),
                tone: "danger",
                icon: "fa-circle-xmark",
                actionText: "Find Another Worker",
                actionUrl: $"/Client-Booking-Details/{booking.Id}",
                needsAttention: true,
                createdBy: booking.CreatedBy);
            await PersistAsync(notification, cancellationToken);
        }

        public async Task StartJobRequestedAsync(Booking booking, CancellationToken cancellationToken)
        {
            var notification = Notification.Create(
                targetId: booking.ClientId,
                bookingId: booking.Id,
                audience: NotificationAudience.Client,
                title: "Confirm the job has started",
                message: $"{booking.Worker?.FullName?.ToString()} says the job is starting. Confirm on your bookings page so it is marked as in progress.",
                status: booking.BookingStatus.ToString(),
                tone: "warning",
                icon: "fa-user-check",
                actionText: "Confirm Job Started",
                actionUrl: $"/Client-Booking-Details/{booking.Id}",
                needsAttention: true,
                createdBy: booking.CreatedBy);
            await PersistAsync(notification, cancellationToken);
        }

        public async Task JobStartedAsync(Booking booking, CancellationToken cancellationToken)
        {
            var notification = Notification.Create(
                targetId: booking.WorkerId,
                bookingId: booking.Id,
                audience: NotificationAudience.Worker,
                title: "Booking is in progress",
                message: $"{booking.Client?.FullName?.ToString()} confirmed the job has started for {booking.ServiceType?.Name ?? "cleaning service"}.",
                status: booking.BookingStatus.ToString(),
                tone: "info",
                icon: "fa-play",
                actionText: "View Booking",
                actionUrl: $"/Worker-Booking-Details/{booking.Id}",
                needsAttention: false,
                createdBy: booking.CreatedBy);
            await PersistAsync(notification, cancellationToken);
        }

        public async Task JobFinishedByWorkerAsync(Booking booking, CancellationToken cancellationToken)
        {
            var notification = Notification.Create(
                targetId: booking.ClientId,
                bookingId: booking.Id,
                audience: NotificationAudience.Client,
                title: "Worker completed the job",
                message: $"{booking.Worker?.FullName?.ToString()} has finished the {booking.ServiceType?.Name ?? "cleaning service"}. Confirm completion to release funds.",
                status: booking.BookingStatus.ToString(),
                tone: "info",
                icon: "fa-check-double",
                actionText: "View Booking",
                actionUrl: $"/Client-Booking-Details/{booking.Id}",
                needsAttention: true,
                createdBy: booking.CreatedBy);
            await PersistAsync(notification, cancellationToken);
        }

        public async Task JobCompletedAsync(Booking booking, CancellationToken cancellationToken)
        {
            var notification = Notification.Create(
                targetId: booking.WorkerId,
                bookingId: booking.Id,
                audience: NotificationAudience.Worker,
                title: "Booking completed",
                message: $"{booking.Client?.FullName?.ToString()} has completed the job. Payments have been released.",
                status: booking.BookingStatus.ToString(),
                tone: "success",
                icon: "fa-circle-check",
                actionText: "View Booking",
                actionUrl: $"/Worker-Booking-Details/{booking.Id}",
                needsAttention: false,
                createdBy: booking.CreatedBy);
            await PersistAsync(notification, cancellationToken);
        }

        public async Task PaymentConfirmedAsync(Booking booking, CancellationToken cancellationToken)
        {
            var notification = Notification.Create(
                targetId: booking.WorkerId,
                bookingId: booking.Id,
                audience: NotificationAudience.Worker,
                title: "Client payment confirmed",
                message: $"{booking.Client?.FullName?.ToString()} has paid for {booking.ServiceType?.Name ?? "the cleaning service"}. You can prepare for the job.",
                status: booking.BookingStatus.ToString(),
                tone: "success",
                icon: "fa-wallet",
                actionText: "View Booking",
                actionUrl: $"/Worker-Booking-Details/{booking.Id}",
                needsAttention: true,
                createdBy: booking.CreatedBy);
            await PersistAsync(notification, cancellationToken);
        }

        public async Task BookingExpiredAsync(Booking booking, CancellationToken cancellationToken)
        {
            var serviceName = booking.ServiceType?.Name ?? "cleaning service";
            var date = booking.DateOfService.ToString("MMM dd, yyyy");

            var clientNotification = Notification.Create(
                targetId: booking.ClientId,
                bookingId: booking.Id,
                audience: NotificationAudience.Client,
                title: "Booking expired",
                message: $"Your booking for {serviceName} on {date} expired because it was not completed by the service date.",
                status: booking.BookingStatus.ToString(),
                tone: "danger",
                icon: "fa-calendar-xmark",
                actionText: "View Booking",
                actionUrl: $"/Client-Booking-Details/{booking.Id}",
                needsAttention: true,
                createdBy: booking.CreatedBy);
            await PersistAsync(clientNotification, cancellationToken);

            var workerNotification = Notification.Create(
                targetId: booking.WorkerId,
                bookingId: booking.Id,
                audience: NotificationAudience.Worker,
                title: "Booking expired",
                message: $"The booking for {booking.Client?.FullName?.ToString() ?? "a client"} on {date} expired because it was not completed by the service date.",
                status: booking.BookingStatus.ToString(),
                tone: "danger",
                icon: "fa-calendar-xmark",
                actionText: "View Booking",
                actionUrl: $"/Worker-Booking-Details/{booking.Id}",
                needsAttention: true,
                createdBy: booking.CreatedBy);
            await PersistAsync(workerNotification, cancellationToken);
        }

        public async Task RefundProcessedAsync(Booking booking, CancellationToken cancellationToken)
        {
            var notification = Notification.Create(
                targetId: booking.ClientId,
                bookingId: booking.Id,
                audience: NotificationAudience.Client,
                title: "Payment refunded",
                message: $"Your payment of \u20A6{booking.Amount.ToString("N0")} for {booking.ServiceType?.Name ?? "the cleaning service"} was refunded because the booking expired before the job was completed.",
                status: booking.BookingStatus.ToString(),
                tone: "success",
                icon: "fa-rotate-left",
                actionText: "View Booking",
                actionUrl: $"/Client-Booking-Details/{booking.Id}",
                needsAttention: true,
                createdBy: booking.CreatedBy);
            await PersistAsync(notification, cancellationToken);
        }

        public async Task ReferralCouponAwardedAsync(Client referrer, string couponCode, CancellationToken cancellationToken)
        {
            var notification = Notification.Create(
                targetId: referrer.Id,
                bookingId: Guid.Empty,
                audience: NotificationAudience.Client,
                title: "Referral reward unlocked",
                message: $"A friend you referred completed their first booking. You earned a 30% discount coupon: {couponCode}. Redeem it on your next booking.",
                status: "Referral",
                tone: "success",
                icon: "fa-gift",
                actionText: "Your Dashboard",
                actionUrl: "/Client-Dashboard",
                needsAttention: true,
                createdBy: referrer.CreatedBy);
            await PersistAsync(notification, cancellationToken);
        }

        private async Task PersistAsync(Notification notification, CancellationToken cancellationToken)
        {
            try
            {
                await _repo.Notifications.CreateAsync(notification, cancellationToken);
                await _repo.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist notification for booking {BookingId}", notification.BookingId);
            }
        }
    }
}