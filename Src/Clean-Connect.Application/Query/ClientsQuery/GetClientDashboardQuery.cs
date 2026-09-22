using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean_Connect.Application.Query.ClientsQuery
{
    public record GetClientDashboardQuery : IRequest<ClientDashboardDto>;

    public record ClientDashboardDto
    {
        public string ClientName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ReferralCode { get; set; } = string.Empty;
        public int SuccessfulReferralCount { get; set; }
        public int TotalJobs { get; set; }
        public int ActiveBookings { get; set; }
        public int CompletedBookings { get; set; }
        public decimal TotalSpent { get; set; }
        public List<ClientRecentBookingItem> RecentBookings { get; set; } = new();
    }

    public record ClientRecentBookingItem
    {
        public Guid BookingId { get; set; }
        public string WorkerName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime DateOfService { get; set; }
        public string BookingStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class GetClientDashboardQueryHandler(IUnitOfWork repo, ICurrentUser currentUser, ILogger<GetClientDashboardQueryHandler> logger) : IRequestHandler<GetClientDashboardQuery, ClientDashboardDto>
    {
        public async Task<ClientDashboardDto> Handle(GetClientDashboardQuery request, CancellationToken cancellationToken)
        {
            var email = currentUser.Email;

            if (string.IsNullOrWhiteSpace(email))
            {
                logger.LogWarning("Client dashboard accessed without email claim.");
                return new ClientDashboardDto { ClientName = "Client" };
            }

            var client = await repo.Clients.GetByEmail(email, cancellationToken);

            if (client == null)
            {
                logger.LogWarning("Client not found for email: {Email}", email);
                return new ClientDashboardDto { ClientName = "Client" };
            }

            var bookings = client.Bookings ?? new();

            var activeStatuses = new[] { "InProgress", "AwaitingClientStartConfirmation", "AcceptedAwaitingPayment", "MarkAsPaid" };

            var recentBookings = bookings
                .OrderByDescending(b => b.DateOfBooking)
                .Take(5)
                .Select(b => new ClientRecentBookingItem
                {
                    BookingId = b.Id,
                    WorkerName = b.Worker?.FullName?.FirstName + " " + b.Worker?.FullName?.LastName,
                    ServiceName = b.ServiceType?.Name ?? "N/A",
                    Address = b.Address?.Value ?? "",
                    DateOfService = b.DateOfService,
                    BookingStatus = b.BookingStatus.ToString(),
                    PaymentStatus = b.PaymentStatus.ToString()
                })
                .ToList();

            return new ClientDashboardDto
            {
                ClientName = $"{client.FullName.FirstName} {client.FullName.LastName}",
                Email = client.Email.Value,
                State = client.State,
                ReferralCode = client.ReferralCode,
                SuccessfulReferralCount = client.SuccessfulReferralCount,
                TotalJobs = bookings.Count,
                ActiveBookings = bookings.Count(b => activeStatuses.Contains(b.BookingStatus.ToString())),
                CompletedBookings = bookings.Count(b => b.BookingStatus == Domain.Enums.BookingStatus.Completed),
                TotalSpent = bookings
                    .Where(b => b.BookingStatus == Domain.Enums.BookingStatus.Completed)
                    .Sum(b => b.Amount),
                RecentBookings = recentBookings
            };
        }
    }
}
