using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Clean_Connect.Application.Interface.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Clean_Connect.Application.Command.Services
{
    public class AgoraChatProvisioningService(
        HttpClient httpClient,
        IOptions<AgoraChatOptions> options,
        IAgoraChatTokenService tokenService,
        ILogger<AgoraChatProvisioningService> logger) : IAgoraChatProvisioningService
    {
        private readonly AgoraChatOptions _options = options.Value;

        public async Task EnsureUsersExistAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            if (!_options.ProvisionUsers)
            {
                return;
            }

            var distinctUserIds = userIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (distinctUserIds.Length == 0)
            {
                return;
            }

            var host = BuildApiHost();
            var appId = RequireConfigured(_options.AppId, nameof(AgoraChatOptions.AppId));
            var appToken = tokenService.CreateAppToken();

            foreach (var userId in distinctUserIds)
            {
                await EnsureUserExistsAsync(host, appId, appToken, userId, cancellationToken);
            }
        }

        private async Task EnsureUserExistsAsync(
            Uri host,
            string appId,
            string appToken,
            string userId,
            CancellationToken cancellationToken)
        {
            var encodedUserId = Uri.EscapeDataString(userId);
            using var lookup = new HttpRequestMessage(HttpMethod.Get, new Uri(host, $"app-id/{appId}/users/{encodedUserId}"));
            lookup.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            lookup.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appToken);

            using var lookupResponse = await httpClient.SendAsync(lookup, cancellationToken);
            if (lookupResponse.IsSuccessStatusCode)
            {
                return;
            }

            if (lookupResponse.StatusCode != HttpStatusCode.NotFound)
            {
                var body = await lookupResponse.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Agora Chat user lookup failed with status {StatusCode}.", (int)lookupResponse.StatusCode);
            }

            using var create = new HttpRequestMessage(HttpMethod.Post, new Uri(host, $"app-id/{appId}/users"));
            create.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appToken);
            create.Content = JsonContent.Create(new { username = userId });

            using var createResponse = await httpClient.SendAsync(create, cancellationToken);
            if (createResponse.IsSuccessStatusCode)
            {
                return;
            }

            var createBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);
            if (IsAlreadyExistsResponse(createResponse.StatusCode, createBody))
            {
                return;
            }

            logger.LogError("Agora Chat user provisioning failed with status {StatusCode}.", (int)createResponse.StatusCode);
            throw new InvalidOperationException("Agora Chat user provisioning failed.");
        }

        private Uri BuildApiHost()
        {
            var host = RequireConfigured(_options.ApiHost, nameof(AgoraChatOptions.ApiHost)).TrimEnd('/');
            if (!host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                host = $"https://{host}";
            }

            return new Uri($"{host}/");
        }

        private static bool IsAlreadyExistsResponse(HttpStatusCode statusCode, string body)
        {
            if (statusCode == HttpStatusCode.Conflict)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                var normalized = document.RootElement.ToString();
                return normalized.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                       normalized.Contains("already", StringComparison.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return body.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                       body.Contains("already", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string RequireConfigured(string value, string settingName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"AgoraChat:{settingName} is not configured.");
            }

            return value;
        }
    }
}
