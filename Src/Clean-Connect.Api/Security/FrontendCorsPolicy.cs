using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Clean_Connect.Api.Security;

public static class FrontendCorsPolicy
{
    public const string Name = "Frontend";
    private static readonly string[] Methods = ["GET", "POST", "PUT", "DELETE"];
    private static readonly string[] Headers = ["Accept", "Authorization", "Content-Type"];

    public static CorsPolicy Build(IEnumerable<string?> configuredOrigins)
    {
        var origins = configuredOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin!.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (origins.Length == 0 || origins.Any(origin => origin == "*" || !IsOrigin(origin)))
            throw new InvalidOperationException("Cors:AllowedOrigins must contain explicit HTTP or HTTPS origins.");

        var builder = new CorsPolicyBuilder()
            .WithOrigins(origins)
            .WithMethods(Methods)
            .WithHeaders(Headers);
        // API authentication uses bearer tokens; cross-origin cookies are not required.
        return builder.Build();
    }

    private static bool IsOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !uri.Host.Contains('*')
            && string.IsNullOrEmpty(uri.UserInfo)
            && uri.AbsolutePath == "/"
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);
    }
}
