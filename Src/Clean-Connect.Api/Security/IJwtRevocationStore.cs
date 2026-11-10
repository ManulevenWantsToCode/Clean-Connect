namespace Clean_Connect.Api.Security;

public interface IJwtRevocationStore
{
    Task RevokeAsync(Guid userId, string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task<bool> IsRevokedAsync(Guid userId, string tokenId, CancellationToken cancellationToken);
}
