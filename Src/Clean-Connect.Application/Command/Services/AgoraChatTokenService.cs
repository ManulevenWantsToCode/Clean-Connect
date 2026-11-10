using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Services;
using Microsoft.Extensions.Options;

namespace Clean_Connect.Application.Command.Services
{
    public class AgoraChatTokenService(IOptions<AgoraChatOptions> options) : IAgoraChatTokenService
    {
        private readonly AgoraChatOptions _options = options.Value;

        public AgoraChatTokenResult CreateUserToken(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("Agora Chat user ID is required.", nameof(userId));
            }

            var ttl = NormalizeTtl(_options.UserTokenTtlSeconds, nameof(AgoraChatOptions.UserTokenTtlSeconds));
            var token = AgoraChatAccessTokenBuilder.BuildUserToken(
                RequireConfigured(_options.AppId, nameof(AgoraChatOptions.AppId)),
                RequireConfigured(_options.AppCertificate, nameof(AgoraChatOptions.AppCertificate)),
                userId,
                (uint)ttl);

            return new AgoraChatTokenResult(
                _options.AppId,
                RequireConfigured(_options.AppKey, nameof(AgoraChatOptions.AppKey)),
                token,
                DateTimeOffset.UtcNow.AddSeconds(ttl),
                ttl);
        }

        public string CreateAppToken()
        {
            var ttl = NormalizeTtl(_options.AppTokenTtlSeconds, nameof(AgoraChatOptions.AppTokenTtlSeconds));

            return AgoraChatAccessTokenBuilder.BuildAppToken(
                RequireConfigured(_options.AppId, nameof(AgoraChatOptions.AppId)),
                RequireConfigured(_options.AppCertificate, nameof(AgoraChatOptions.AppCertificate)),
                (uint)ttl);
        }

        private static int NormalizeTtl(int ttlSeconds, string settingName)
        {
            if (ttlSeconds <= 0 || ttlSeconds > AgoraChatOptions.MaxTokenTtlSeconds)
            {
                throw new InvalidOperationException($"{settingName} must be between 1 and {AgoraChatOptions.MaxTokenTtlSeconds} seconds.");
            }

            return ttlSeconds;
        }

        private static string RequireConfigured(string value, string settingName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"AgoraChat:{settingName} is not configured.");
            }

            return value.Trim();
        }
    }
}
