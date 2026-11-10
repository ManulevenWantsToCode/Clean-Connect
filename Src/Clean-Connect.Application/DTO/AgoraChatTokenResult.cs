namespace Clean_Connect.Application.DTO
{
    public record AgoraChatTokenResult(
        string AppId,
        string AppKey,
        string AccessToken,
        DateTimeOffset ExpiresAtUtc,
        int TtlSeconds);
}
