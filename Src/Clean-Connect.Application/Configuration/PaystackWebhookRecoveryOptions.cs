namespace Clean_Connect.Application.Configuration;

public sealed class PaystackWebhookRecoveryOptions
{
    public const string SectionName = "Paystack:WebhookRecovery";
    public int PollIntervalSeconds { get; set; } = 30;
    public int InitialDelaySeconds { get; set; } = 30;
    public int MaximumDelaySeconds { get; set; } = 3600;
    public int MaximumAttempts { get; set; } = 8;
    public int ProcessingLeaseSeconds { get; set; } = 180;
    public int BatchSize { get; set; } = 25;

    public TimeSpan RetryDelay(int retryCount)
    {
        if (retryCount < 1)
            throw new ArgumentOutOfRangeException(nameof(retryCount));
        var exponent = Math.Min(retryCount - 1, 30);
        var seconds = Math.Min(MaximumDelaySeconds, InitialDelaySeconds * (1L << exponent));
        return TimeSpan.FromSeconds(seconds);
    }

    public void Validate()
    {
        if (PollIntervalSeconds <= 0 || InitialDelaySeconds <= 0 || MaximumDelaySeconds < InitialDelaySeconds
            || MaximumAttempts <= 0 || ProcessingLeaseSeconds <= 0 || BatchSize is < 1 or > 500)
            throw new InvalidOperationException("Paystack webhook recovery configuration is invalid.");
    }
}
