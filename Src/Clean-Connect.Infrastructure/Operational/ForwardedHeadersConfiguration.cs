using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clean_Connect.Infrastructure.Operational;

public static class ForwardedHeadersConfiguration
{
    public static IServiceCollection AddTrustedForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = Math.Clamp(configuration.GetValue("ForwardedHeaders:ForwardLimit", 1), 1, 5);
            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
                if (IPAddress.TryParse(proxy, out var address)) options.KnownProxies.Add(address);
        });
        return services;
    }
}
