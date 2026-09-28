namespace Clean_Connect.Application.DTO
{
    /// <summary>
    /// Request body for triggering a payout on a completed booking using the worker's stored bank details.
    /// </summary>
    public record PayoutRequest(
        Guid WorkerId,
        string? ModifiedBy = null);
}