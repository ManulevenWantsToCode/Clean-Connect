namespace Clean_Connect.Domain.Entities;

/// <summary>Single-row, administrator-editable platform configuration.</summary>
public class PlatformSettings
{
    public int Id { get; set; } = 1;
    public string PlatformName { get; set; } = "Clean Connect";
    public string SupportEmail { get; set; } = "support@cleanconnect.com";
    public string CustomerServicePhone { get; set; } = "+234 800 253 2626";
    public string BaseCurrency { get; set; } = "NGN";
    public string Timezone { get; set; } = "Africa/Lagos";
    public bool MaintenanceMode { get; set; }
    public int AutoMatchCleanerTimeoutMinutes { get; set; } = 15;
    public int MinimumAdvanceNoticeHours { get; set; } = 2;
    public int FreeCancellationWindowHours { get; set; } = 4;
    public int MaxActiveJobsPerCleanerPerDay { get; set; } = 2;
    public decimal CommissionPercent { get; set; } = 15m;
    public int AutoReleaseEscrowDelayHours { get; set; } = 24;
    public decimal MinimumPayoutAmount { get; set; }
    public bool EmailAlertsOnNewBooking { get; set; } = true;
    public bool SmsToCleanerOnAssignment { get; set; } = true;
    public bool EmailBookingConfirmation { get; set; } = true;
    public bool EmailBookingCancellation { get; set; } = true;
    public bool EmailPaymentNotifications { get; set; } = true;
    public bool SmsBookingNotifications { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UnixEpoch;
}
