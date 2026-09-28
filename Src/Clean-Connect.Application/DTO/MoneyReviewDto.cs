namespace Clean_Connect.Application.DTO
{
    public class MoneyReviewDto
    {
        public bool IsWorker { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal ThisMonthAmount { get; set; }
        public decimal PreviousMonthAmount { get; set; }
        public int PaidTransactionCount { get; set; }
        public decimal AveragePerBooking { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal CommissionTotal { get; set; }
        public List<MonthlyMoneyItem> MonthlySeries { get; set; } = new();
        public List<CategoryMoneyItem> CategoryBreakdown { get; set; } = new();
        public List<MoneyTransactionItem> Transactions { get; set; } = new();
    }

    public class MonthlyMoneyItem
    {
        public string Label { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Amount { get; set; }
        public bool IsCurrent { get; set; }
        public double Percentage { get; set; }
    }

    public class CategoryMoneyItem
    {
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class MoneyTransactionItem
    {
        public Guid BookingId { get; set; }
        public string CounterpartyName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string BookingStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal Fee { get; set; }
    }
}