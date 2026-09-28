using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Interface.Services;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Clean_Connect.Application.Query.MoneyQuery
{
    public record GetMoneyReviewQuery : IRequest<MoneyReviewDto>;

    public class GetMoneyReviewQueryHandler(
        IUnitOfWork repo,
        ICurrentUser currentUser,
        ILogger<GetMoneyReviewQueryHandler> logger) : IRequestHandler<GetMoneyReviewQuery, MoneyReviewDto>
    {
        public async Task<MoneyReviewDto> Handle(GetMoneyReviewQuery request, CancellationToken ct)
        {
            var email = currentUser.Email;
            if (string.IsNullOrWhiteSpace(email))
                throw new UnauthorizedAccessException("User is not authenticated.");

            var worker = await repo.Workers.GetByEmail(email, ct);
            if (worker != null)
                return await BuildWorkerReview(worker.Id, worker.FullName?.ToString() ?? "Worker", ct);

            var client = await repo.Clients.GetByEmail(email, ct);
            if (client != null)
                return BuildClientReview(client);

            logger.LogWarning("Money review requested for user without a profile: {Email}", email);
            throw new KeyNotFoundException("No client or worker profile found for the current user.");
        }

        private async Task<MoneyReviewDto> BuildWorkerReview(Guid workerId, string name, CancellationToken ct)
        {
            var workerFull = await repo.Workers.GetWorkerById(workerId, ct) ?? throw new KeyNotFoundException("Worker profile not found.");
            var escrows = await repo.Escrows.GetByWorkerId(workerId, ct);
            var escrowByBooking = escrows.ToDictionary(e => e.BookingId);

            var items = workerFull.Bookings
                .Where(b => b.PaymentStatus == PaymentStatus.Successful && b.BookingStatus == BookingStatus.Completed)
                .Select(b =>
                {
                    var escrow = escrowByBooking.GetValueOrDefault(b.Id);
                    var gross = b.Amount;
                    var fee = escrow?.CommissionAmount ?? 0m;
                    return new MoneyTransactionItem
                    {
                        BookingId = b.Id,
                        CounterpartyName = b.Client?.FullName?.ToString() ?? "Unknown client",
                        ServiceName = b.ServiceType?.Name ?? "General Cleaning",
                        Date = b.DateOfBooking,
                        BookingStatus = b.BookingStatus.ToString(),
                        PaymentStatus = b.PaymentStatus.ToString(),
                        Amount = gross - fee,
                        GrossAmount = gross,
                        Fee = fee
                    };
                })
                .ToList();

            var dto = BuildReview(true, name, items);
            dto.GrossAmount = items.Sum(i => i.GrossAmount);
            dto.CommissionTotal = items.Sum(i => i.Fee);
            return dto;
        }

        private static MoneyReviewDto BuildClientReview(Client client)
        {
            var items = client.Bookings
                .Where(b => b.PaymentStatus == PaymentStatus.Successful)
                .Select(b => new MoneyTransactionItem
                {
                    BookingId = b.Id,
                    CounterpartyName = b.Worker?.FullName?.ToString() ?? "Unknown worker",
                    ServiceName = b.ServiceType?.Name ?? "General Cleaning",
                    Date = b.DateOfBooking,
                    BookingStatus = b.BookingStatus.ToString(),
                    PaymentStatus = b.PaymentStatus.ToString(),
                    Amount = b.Amount,
                    GrossAmount = b.Amount,
                    Fee = 0m
                })
                .ToList();

            var dto = BuildReview(false, $"{client.FullName.FirstName} {client.FullName.LastName}", items);
            dto.GrossAmount = items.Sum(i => i.GrossAmount);
            dto.CommissionTotal = items.Sum(i => i.Fee);
            return dto;
        }

        private static MoneyReviewDto BuildReview(bool isWorker, string name, List<MoneyTransactionItem> items)
        {
            var ordered = items.OrderByDescending(i => i.Date).ToList();
            var total = ordered.Sum(i => i.Amount);
            var count = ordered.Count;
            var today = DateTime.Today;

            var seriesItems = new List<MonthlyMoneyItem>();
            for (var offset = 5; offset >= 0; offset--)
            {
                var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-offset);
                var amount = ordered
                    .Where(i => i.Date.Year == monthStart.Year && i.Date.Month == monthStart.Month)
                    .Sum(i => i.Amount);

                seriesItems.Add(new MonthlyMoneyItem
                {
                    Label = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(monthStart.Month),
                    Year = monthStart.Year,
                    Month = monthStart.Month,
                    Amount = amount,
                    IsCurrent = offset == 0
                });
            }

            var maxAmount = seriesItems.Count > 0 ? seriesItems.Max(s => s.Amount) : 0m;
            foreach (var slot in seriesItems)
                slot.Percentage = maxAmount <= 0 ? 0 : Math.Round((double)(slot.Amount / maxAmount * 100m), 1);

            var currentMonth = new DateTime(today.Year, today.Month, 1);
            var previousMonth = currentMonth.AddMonths(-1);

            var categories = ordered
                .GroupBy(i => i.ServiceName)
                .Select(g => new CategoryMoneyItem
                {
                    Name = g.Key,
                    Amount = g.Sum(i => i.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            foreach (var c in categories)
                c.Percentage = total <= 0 ? 0 : Math.Round((double)(c.Amount / total * 100m), 1);

            return new MoneyReviewDto
            {
                IsWorker = isWorker,
                UserName = name,
                TotalAmount = total,
                ThisMonthAmount = ordered
                    .Where(i => i.Date.Year == currentMonth.Year && i.Date.Month == currentMonth.Month)
                    .Sum(i => i.Amount),
                PreviousMonthAmount = ordered
                    .Where(i => i.Date.Year == previousMonth.Year && i.Date.Month == previousMonth.Month)
                    .Sum(i => i.Amount),
                PaidTransactionCount = count,
                AveragePerBooking = count > 0 ? Math.Round(total / count, 2) : 0m,
                MonthlySeries = seriesItems,
                CategoryBreakdown = categories,
                Transactions = ordered
            };
        }
    }
}