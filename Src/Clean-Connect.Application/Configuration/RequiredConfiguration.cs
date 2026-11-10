using Microsoft.Extensions.Configuration;

namespace Clean_Connect.Application.Configuration;

public static class RequiredConfiguration
{
    private static readonly string[] ProductionRequiredKeys =
    [
        "ConnectionStrings:DefaultConnection",
        "Jwt:Key",
        "Jwt:Issuer",
        "Jwt:Audience",
        "Email:Smtp:Username",
        "Email:Smtp:Password",
        "Email:Smtp:From",
        "Paystack:SecretKey",
        "Geocoding:ApiKey",
        "AgoraChat:AppId",
        "AgoraChat:AppKey",
        "AgoraChat:AppCertificate"
    ];

    public static void ValidateProduction(IConfiguration configuration, string environmentName)
    {
        if (!string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
            return;

        var missing = ProductionRequiredKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToArray();

        if (missing.Length > 0)
            throw new InvalidOperationException(
                $"Required production configuration is missing: {string.Join(", ", missing)}.");

        var googleClientId = configuration["Authentication:Google:ClientId"];
        var googleClientSecret = configuration["Authentication:Google:ClientSecret"];
        if (string.IsNullOrWhiteSpace(googleClientId) != string.IsNullOrWhiteSpace(googleClientSecret))
            throw new InvalidOperationException(
                "Google authentication requires both Authentication:Google:ClientId and Authentication:Google:ClientSecret.");
    }
}
