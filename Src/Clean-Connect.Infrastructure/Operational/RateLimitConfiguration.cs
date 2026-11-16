using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clean_Connect.Infrastructure.Operational;

public static class RateLimitConfiguration
{
    public const string Authentication = "authentication";
    public const string Sensitive = "sensitive";
    public const string PublicRead = "public-read";

    public static IServiceCollection AddConfiguredRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    : 60;
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };

            AddPolicy(options, configuration, Authentication, "RateLimiting:Authentication", 10, 60);
            AddPolicy(options, configuration, Sensitive, "RateLimiting:Sensitive", 30, 60);
            AddPolicy(options, configuration, PublicRead, "RateLimiting:PublicRead", 120, 60);
        });
        return services;
    }

    private static void AddPolicy(Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options,
        IConfiguration configuration, string policy, string section, int defaultLimit, int defaultWindow)
    {
        var settings = ReadSettings(configuration, section, defaultLimit, defaultWindow);
        options.AddPolicy(policy, context => CreatePartition(context, settings.PermitLimit, settings.WindowSeconds));
    }

    public static RateLimitSettings ReadSettings(IConfiguration configuration, string section,
        int defaultLimit, int defaultWindow) => new(
        Math.Clamp(configuration.GetValue($"{section}:PermitLimit", defaultLimit), 1, 10000),
        Math.Clamp(configuration.GetValue($"{section}:WindowSeconds", defaultWindow), 1, 3600));

    public static RateLimitPartition<string> CreatePartition(HttpContext context, int permitLimit, int windowSeconds) =>
        RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Clamp(permitLimit, 1, 10000),
            Window = TimeSpan.FromSeconds(Math.Clamp(windowSeconds, 1, 3600)),
            QueueLimit = 0,
            AutoReplenishment = true
        });

    public static string GetPartitionKey(HttpContext context)
    {
        var authenticatedId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(authenticatedId))
            return $"user:{authenticatedId}";
        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}

public sealed record RateLimitSettings(int PermitLimit, int WindowSeconds);
