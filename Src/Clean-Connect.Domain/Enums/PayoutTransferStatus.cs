namespace Clean_Connect.Domain.Enums
{
    public enum PayoutTransferStatus
    {
        NotStarted,
        Initiating,
        Pending,
        OtpRequired,
        Unknown,
        Failed,
        Reversed,
        Succeeded,
        LegacyUnverified
    }
}
