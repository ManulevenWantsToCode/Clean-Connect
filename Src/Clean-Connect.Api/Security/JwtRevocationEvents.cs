using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Clean_Connect.Api.Security;

public static class JwtRevocationEvents
{
    public static async Task ValidateTokenAsync(TokenValidatedContext context)
    {
        var userValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var tokenId = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (!Guid.TryParse(userValue, out var userId) || string.IsNullOrWhiteSpace(tokenId))
        {
            context.Fail("Invalid token session.");
            return;
        }

        try
        {
            var store = context.HttpContext.RequestServices.GetRequiredService<IJwtRevocationStore>();
            if (await store.IsRevokedAsync(userId, tokenId, context.HttpContext.RequestAborted))
                context.Fail("Token has been revoked.");
        }
        catch (Exception ex)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtRevocation");
            logger.LogError(ex, "JWT revocation check failed; rejecting authenticated request.");
            context.Fail("Authentication could not be verified.");
        }
    }
}
