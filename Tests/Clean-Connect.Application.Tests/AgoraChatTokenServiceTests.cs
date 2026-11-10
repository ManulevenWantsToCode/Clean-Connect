using Clean_Connect.Application.Command.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Clean_Connect.Application.Tests
{
    public class AgoraChatTokenServiceTests
    {
        [Fact]
        public void CreateUserToken_WithValidConfiguration_ReturnsShortLivedToken()
        {
            var service = CreateService();

            var result = service.CreateUserToken("client-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", result.AppId);
            Assert.Equal("cleanconnect#test", result.AppKey);
            Assert.StartsWith("007", result.AccessToken);
            Assert.Equal(600, result.TtlSeconds);
            Assert.True(result.ExpiresAtUtc > DateTimeOffset.UtcNow);
        }

        [Fact]
        public void CreateUserToken_WhenTtlExceedsAgoraMaximum_ThrowsInvalidOperationException()
        {
            var service = CreateService(options => options.UserTokenTtlSeconds = AgoraChatOptions.MaxTokenTtlSeconds + 1);

            Assert.Throws<InvalidOperationException>(() => service.CreateUserToken("worker-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
        }

        [Fact]
        public void CreateUserToken_WhenCertificateIsMissing_ThrowsInvalidOperationException()
        {
            var service = CreateService(options => options.AppCertificate = string.Empty);

            Assert.Throws<InvalidOperationException>(() => service.CreateUserToken("worker-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
        }

        private static AgoraChatTokenService CreateService(Action<AgoraChatOptions>? configure = null)
        {
            var options = new AgoraChatOptions
            {
                AppId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                AppKey = "cleanconnect#test",
                AppCertificate = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                UserTokenTtlSeconds = 600,
                AppTokenTtlSeconds = 600
            };

            configure?.Invoke(options);

            return new AgoraChatTokenService(Options.Create(options));
        }
    }
}
