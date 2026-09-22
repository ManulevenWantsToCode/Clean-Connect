namespace Clean_Connect.Application.DTO
{
    public record WorkerDashboardDto
    {
        public string WorkerName { get; set; }
        public string Email { get; set; }
        public string ServiceType { get; set; }
        public string State { get; set; }
        public bool IsAvailable { get; set; }
        public double AverageRating { get; set; }
        public int TotalRating { get; set; }

        public int TotalJobs { get; set; }
        public int ActiveBookings { get; set; }
        public int PendingBookings { get; set; }
        public decimal TotalEarnings { get; set; }
        public decimal WalletBalance { get; set; }

        public int FiveStarCount { get; set; }
        public int FourStarCount { get; set; }
        public int ThreeStarCount { get; set; }
        public int TwoStarCount { get; set; }
        public int OneStarCount { get; set; }

        public List<RecentBookingItem> RecentBookings { get; set; } = new();
    }

    public record RecentBookingItem
    {
        public Guid BookingId { get; set; }
        public string ClientName { get; set; }
        public string ServiceName { get; set; }
        public string Address { get; set; }
        public DateTime DateOfService { get; set; }
        public DateTime DateOfBooking { get; set; }
        public string TimeRange { get; set; }
        public string BookingStatus { get; set; }
        public string PaymentStatus { get; set; }
    }
}
