using Clean_Connect.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Clean_Connect.Api.Security;

public sealed class IdentityJwtRevocationStore(ApplicationDbContext db) : IJwtRevocationStore
{
    private const string Provider = "CleanConnectJwt";
    private const string Prefix = "jti:";

    public async Task RevokeAsync(Guid userId, string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var userRevocations = await db.UserTokens
            .Where(x => x.UserId == userId && x.LoginProvider == Provider)
            .ToListAsync(cancellationToken);
        foreach (var expired in userRevocations.Where(x => DateTimeOffset.TryParse(x.Value, out var expiry) && expiry <= now))
            db.UserTokens.Remove(expired);

        var name = Prefix + tokenId;
        var row = userRevocations.SingleOrDefault(x => x.Name == name);
        if (row is null)
            db.UserTokens.Add(new IdentityUserToken<Guid> { UserId = userId, LoginProvider = Provider, Name = name, Value = expiresAt.ToUniversalTime().ToString("O") });
        else
            row.Value = expiresAt.ToUniversalTime().ToString("O");

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsRevokedAsync(Guid userId, string tokenId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var match = await db.UserTokens.SingleOrDefaultAsync(x => x.UserId == userId && x.LoginProvider == Provider && x.Name == Prefix + tokenId, cancellationToken);
        if (match is not null && DateTimeOffset.TryParse(match.Value, out var expiry) && expiry <= now)
        {
            db.UserTokens.Remove(match);
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }
        return match is not null && DateTimeOffset.TryParse(match.Value, out var matchExpiry) && matchExpiry > now;
    }
}
