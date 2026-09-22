using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Query.WorkersQuery
{
    public record GetWorkerDashboardQuery : IRequest<WorkerDashboardDto>;

    public class GetWorkerDashboardQueryHandler(
        IUnitOfWork repo,
        ICurrentUser currentUser,
        ILogger<GetWorkerDashboardQueryHandler> logger) : IRequestHandler<GetWorkerDashboardQuery, WorkerDashboardDto>
    {
        public async Task<WorkerDashboardDto> Handle(GetWorkerDashboardQuery request, CancellationToken ct)
        {
            var email = currentUser.Email;
            if (string.IsNullOrWhiteSpace(email))
                throw new UnauthorizedAccessException("User is not authenticated.");

            var worker = await repo.Workers.GetByEmail(email, ct);
            if (worker == null)
                throw new KeyNotFoundException("Worker profile not found for the current user.");

            var workerFull = await repo.Workers.GetWorkerById(worker.Id, ct);
            var wallet = await repo.Wallets.GetByWorkerId(worker.Id, ct);
            var escrows = await repo.Escrows.GetByWorkerId(worker.Id, ct);

            var bookings = workerFull.Bookings ?? new();
            var ratings = workerFull.Ratings?.ToList() ?? new List<Ratings>();

            var totalJobs = bookings.Count(b => b.BookingStatus == BookingStatus.Completed);
            var activeBookings = bookings.Count(b =>
                b.BookingStatus == BookingStatus.InProgress ||
                b.BookingStatus == BookingStatus.AcceptedAwaitingPayment ||
                b.BookingStatus == BookingStatus.MarkAsPaid ||
                b.BookingStatus == BookingStatus.AwaitingClientStartConfirmation);
            var pendingBookings = bookings.Count(b => b.BookingStatus == BookingStatus.Pending);

            var totalEarnings = bookings
                .Where(b => b.BookingStatus == BookingStatus.Completed)
                .Sum(b =>
                {
                    var escrow = escrows.FirstOrDefault(e => e.BookingId == b.Id);
                    return escrow?.WorkerShare ?? b.Amount;
                });

            var recentBookings = bookings
                .OrderByDescending(b => b.DateOfBooking)
                .Take(5)
                .Select(b => new RecentBookingItem
                {
                    BookingId = b.Id,
                    ClientName = b.Client?.FullName?.ToString() ?? "Unknown",
                    ServiceName = b.ServiceType?.Name ?? "General Cleaning",
                    Address = b.Address?.ToString() ?? "",
                    DateOfService = b.DateOfService,
                    DateOfBooking = b.DateOfBooking,
                    TimeRange = b.TimeRange.ToString(),
                    BookingStatus = b.BookingStatus.ToString(),
                    PaymentStatus = b.PaymentStatus.ToString()
                }).ToList();

            var ratingDistribution = ratings
                .GroupBy(r => r.RatingValue)
                .ToDictionary(g => g.Key, g => g.Count());

            var dto = new WorkerDashboardDto
            {
                WorkerName = worker.FullName?.ToString() ?? "Worker",
                Email = worker.Email?.Value ?? "",
                ServiceType = workerFull.ServiceType?.Name ?? "",
                State = worker.State ?? "",
                IsAvailable = worker.IsAvailable,
                AverageRating = worker.AverageRating,
                TotalRating = worker.TotalRating,
                TotalJobs = totalJobs,
                ActiveBookings = activeBookings,
                PendingBookings = pendingBookings,
                TotalEarnings = totalEarnings,
                WalletBalance = wallet?.Balance ?? 0,
                FiveStarCount = ratingDistribution.GetValueOrDefault(5, 0),
                FourStarCount = ratingDistribution.GetValueOrDefault(4, 0),
                ThreeStarCount = ratingDistribution.GetValueOrDefault(3, 0),
                TwoStarCount = ratingDistribution.GetValueOrDefault(2, 0),
                OneStarCount = ratingDistribution.GetValueOrDefault(1, 0),
                RecentBookings = recentBookings
            };

            logger.LogInformation("Worker dashboard loaded for {Email}: {Name}", email, dto.WorkerName);
            return dto;
        }
    }
}
