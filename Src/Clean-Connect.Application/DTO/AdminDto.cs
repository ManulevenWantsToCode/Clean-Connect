using Clean_Connect.Domain.Enums;

namespace Clean_Connect.Application.DTO
{
    public record AdminDashboardDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal RevenueThisMonth { get; set; }
        public decimal PlatformFeeShare { get; set; }
        public int ActiveBookings { get; set; }
        public int ScheduledToday { get; set; }
        public int ActiveWorkers { get; set; }
        public int PendingWorkers { get; set; }
        public int TotalClients { get; set; }
        public int NewClientsThisMonth { get; set; }
        public decimal EscrowHeld { get; set; }
        public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
        public List<AdminRecentBookingDto> RecentBookings { get; set; } = new();
        public List<AuditActivityDto> AuditActivity { get; set; } = new();
    }

    public record MonthlyRevenueDto
    {
        public string Month { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public double Percentage { get; set; }
    }

    public record AdminRecentBookingDto
    {
        public string BookingId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientInitials { get; set; } = string.Empty;
        public string ClientLocation { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;
        public double? WorkerRating { get; set; }
        public string ServiceType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DateOfService { get; set; }
        public string Address { get; set; } = string.Empty;
        public string TimeRange { get; set; } = string.Empty;
        public decimal? TotalPaid { get; set; }
        public string EscrowStatus { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string WorkerPhone { get; set; } = string.Empty;
    }

    public record AuditActivityDto
    {
        public string IconVariant { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
    }

    public record AdminClientDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int TotalBookings { get; set; }
        public string Status { get; set; } = "Active";
        public decimal TotalSpent { get; set; }
        public DateTime DateJoined { get; set; }
        public string PreferredService { get; set; } = string.Empty;
        public List<ClientRecentBookingDto> RecentBookings { get; set; } = new();
    }

    public record ClientRecentBookingDto
    {
        public string BookingId { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Worker { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public record AdminWorkerDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public double? AverageRating { get; set; }
        public int RatingCount { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public int CompletedJobs { get; set; }
        public string KycStatus { get; set; } = "Pending KYC";
        public string Availability { get; set; } = "Available";
        public bool HasBankDetails { get; set; }
        public string PayoutAccount { get; set; } = string.Empty;
        public DateTime DateJoined { get; set; }
        public string City { get; set; } = string.Empty;
    }

    public record AdminPayoutDto
    {
        public Guid BookingId { get; set; }
        public string BookingRef { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;
        public string WorkerInitials { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public decimal Gross { get; set; }
        public decimal Commission { get; set; }
        public decimal Net { get; set; }
        public bool HasBankDetails { get; set; }
        public string PayoutAccount { get; set; } = string.Empty;
        public string PayoutStatus { get; set; } = string.Empty;
    }

    public record AdminServiceTypeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public int TotalBookings { get; set; }
        public int WorkerCount { get; set; }
        public decimal Revenue { get; set; }
        public string Icon { get; set; } = "fa-house-chimney";
    }

    public record AdminServiceTypeInput
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public record AdminBookingDto
    {
        public Guid Id { get; set; }
        public string BookingId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientInitials { get; set; } = string.Empty;
        public string ClientLocation { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;
        public string WorkerInitials { get; set; } = string.Empty;
        public double? WorkerRating { get; set; }
        public string WorkerPhone { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public DateTime DateOfService { get; set; }
        public string TimeRange { get; set; } = string.Empty;
        public BookingStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string EscrowStatus { get; set; } = string.Empty;
        public decimal Commission { get; set; }
        public decimal WorkerNetPayout { get; set; }
        public string Address { get; set; } = string.Empty;
        public string SpecialInstructions { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
    }

    public record AdminPaymentDto
    {
        public Guid Id { get; set; }
        public string TransactionRef { get; set; } = string.Empty;
        public string BookingRef { get; set; } = string.Empty;
        public string PayerName { get; set; } = string.Empty;
        public string PayeeName { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal Fee { get; set; }
        public string EscrowStatus { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string GatewayRef { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public decimal WorkerNetSettlement { get; set; }
        public string PayoutDestination { get; set; } = string.Empty;
        public PaymentStatus Status { get; set; }
    }

    public record AdminReviewDto
    {
        public Guid Id { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientInitials { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;
        public string WorkerInitials { get; set; } = string.Empty;
        public string WorkerId { get; set; } = string.Empty;
        public int RatingValue { get; set; }
        public string ServiceType { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string ModerationStatus { get; set; } = "Approved";
        public DateTime Date { get; set; }
    }

    public record AdminSettingsDto
    {
        public string PlatformName { get; set; } = "Clean Connect";
        public string SupportEmail { get; set; } = "support@cleanconnect.com";
        public string CustomerServicePhone { get; set; } = "+234 800 253 2626";
        public string BaseCurrency { get; set; } = "NGN (₦) - Nigerian Naira";
        public string Timezone { get; set; } = "WAT";
        public bool MaintenanceMode { get; set; }
        public int AutoMatchCleanerTimeoutMinutes { get; set; } = 15;
        public int MinimumAdvanceNoticeHours { get; set; } = 2;
        public int FreeCancellationWindowHours { get; set; } = 4;
        public int MaxActiveJobsPerCleanerPerDay { get; set; } = 2;
        public int CommissionPercent { get; set; } = 15;
        public int AutoReleaseEscrowDelayHours { get; set; } = 24;
        public string PaymentGatewayMode { get; set; } = "Paystack Live Production";
        public bool EmailAlertsOnNewBooking { get; set; } = true;
        public bool SmsToCleanerOnAssignment { get; set; } = true;
        public bool DisputeAndFlaggedReviewNotifications { get; set; } = true;
        public List<AdminUserDto> AdminUsers { get; set; } = new();
    }

    public record AdminUserDto
    {
        public string Name { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public bool IsOwner { get; set; }
    }

    public record AdminNotificationDto
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Variant { get; set; } = "sky";
        public string TimeAgo { get; set; } = "now";
        public DateTime When { get; set; }
    }
}