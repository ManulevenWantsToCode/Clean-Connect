namespace Clean_Connect.Domain.Enums
{
    public enum BookingStatus
    {
        Pending,
        AcceptedAwaitingPayment,
        MarkAsPaid,
        AwaitingClientStartConfirmation,
        InProgress,
        AwaitingClientConfirmation,
        Rejected,
        Cancelled,
        Completed,
        Expired
    }
}
