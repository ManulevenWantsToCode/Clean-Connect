using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Clean_Connect.Application.Command.Services;
using Clean_Connect.Application.Configuration;
using Clean_Connect.Application.DTO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Clean_Connect.Application.Tests;

public sealed class SecretsAndSensitiveLoggingTests
{
    [Fact]
    public void ProductionConfiguration_FailsWithMissingKeyNamesOnly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "must-not-appear-in-errors"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => RequiredConfiguration.ValidateProduction(configuration, "Production"));

        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message);
        Assert.DoesNotContain("must-not-appear-in-errors", exception.Message);
    }

    [Fact]
    public void ProductionConfiguration_AcceptsCompleteSecretProviderConfiguration()
    {
        var requiredKeys = new[]
        {
            "ConnectionStrings:DefaultConnection", "Jwt:Key", "Jwt:Issuer", "Jwt:Audience",
            "Email:Smtp:Username", "Email:Smtp:Password", "Email:Smtp:From", "Paystack:SecretKey",
            "Geocoding:ApiKey", "AgoraChat:AppId", "AgoraChat:AppKey", "AgoraChat:AppCertificate"
        };
        var values = requiredKeys.ToDictionary(key => key, _ => (string?)"configured-test-value");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        RequiredConfiguration.ValidateProduction(configuration, "Production");
    }

    [Fact]
    public void TrackedAppSettingsTemplates_ContainNoSecretValues()
    {
        var root = FindRepositoryRoot();
        var secretPaths = new[]
        {
            "ConnectionStrings:DefaultConnection", "Jwt:Key", "Email:Smtp:Username", "Email:Smtp:Password",
            "Email:Smtp:From", "Paystack:SecretKey", "Paystack:WebhookSecret", "Geocoding:ApiKey",
            "AgoraChat:AppId", "AgoraChat:AppKey", "AgoraChat:AppCertificate", "Admin:SetupKey",
            "Authentication:Google:ClientSecret"
        };
        var appSettingsFiles = new[]
        {
            "Src/Clean-Connect.Api/appsettings.json", "Src/Clean-Connect.Api/appsettings.Development.json",
            "Src/Clean-Connect.Web/appsettings.json", "Src/Clean-Connect.Web/appsettings.Development.json"
        };

        foreach (var relativePath in appSettingsFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, relativePath)));
            foreach (var secretPath in secretPaths)
            {
                var value = document.RootElement;
                var found = true;
                foreach (var segment in secretPath.Split(':'))
                {
                    if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out var child))
                    {
                        found = false;
                        break;
                    }
                    value = child;
                }

                Assert.True(!found || value.ValueKind == JsonValueKind.Null
                    || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())),
                    $"Secret value must not be stored at {relativePath}:{secretPath}.");
            }
        }
    }

    [Fact]
    public void EnvironmentVariableProvider_BindsNestedSecretKeys()
    {
        const string environmentKey = "CC_TEST_Jwt__Key";
        var previous = Environment.GetEnvironmentVariable(environmentKey);
        try
        {
            Environment.SetEnvironmentVariable(environmentKey, "environment-test-signing-key");
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables("CC_TEST_").Build();
            Assert.Equal("environment-test-signing-key", configuration["Jwt:Key"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentKey, previous);
        }
    }

    [Fact]
    public async Task Geocoding_UsesConfiguredKeyWithoutLoggingIt()
    {
        const string apiKey = "test-geocoding-key-not-for-production";
        var handler = new CapturingHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"results\":[{\"formatted\":\"Lagos, Nigeria\"}]}", Encoding.UTF8, "application/json")
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Geocoding:ApiKey"] = apiKey
        }).Build();
        var service = new GeocodingService(new HttpClient(handler), config);

        var result = await service.GetAddressAsync(6.5, 3.4);

        Assert.Equal("Lagos, Nigeria", result);
        Assert.Contains(apiKey, handler.RequestUri!.Query);
        Assert.Throws<InvalidOperationException>(() => new GeocodingService(new HttpClient(), new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void GeocodingAndJwtSource_DoNotEmbedKeysOrLogTokens()
    {
        var root = FindRepositoryRoot();
        var geocodingSource = File.ReadAllText(Path.Combine(root, "Src", "Clean-Connect.Application", "Command", "Services", "GeocodingService.cs"));
        var jwtSource = File.ReadAllText(Path.Combine(root, "Src", "Clean-Connect.Application", "Command", "Auth", "JwtTokenCommand.cs"));

        Assert.Contains("Geocoding:ApiKey", geocodingSource);
        Assert.DoesNotMatch(new Regex(@"\b[a-f0-9]{32}\b", RegexOptions.IgnoreCase), geocodingSource);
        Assert.DoesNotContain("LogInformation", jwtSource);
        Assert.DoesNotContain("LogDebug", jwtSource);
    }

    [Fact]
    public async Task PaystackLogs_DoNotIncludePaymentCredentialsOrProviderPayloads()
    {
        const string apiSecret = "test-paystack-secret-not-for-production";
        const string email = "sensitive-customer@example.test";
        const string reference = "test-payment-reference-sensitive";
        const string callback = "https://example.test/payment/callback?token=sensitive";
        const string accountNumber = "1234567890";
        var handler = new CapturingHttpHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                request.RequestUri!.AbsolutePath.EndsWith("transferrecipient", StringComparison.Ordinal)
                    ? "{\"status\":true,\"data\":{\"recipient_code\":\"test-recipient-code\"}}"
                    : "{\"status\":true,\"data\":{\"authorization_url\":\"https://checkout.example.test\",\"reference\":\"test-payment-reference-sensitive\"}}",
                Encoding.UTF8,
                "application/json")
        });
        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = apiSecret
        }).Build();
        var service = new PaystackService(new HttpClient(handler), config, loggerFactory.CreateLogger<PaystackService>());

        await service.InitializePayment(12345m, email, reference, callback);
        await service.CreateTransferRecipientAsync(new WorkerBankAccountDto(accountNumber, "011", "Sensitive Account Name"), CancellationToken.None);

        var logs = string.Join("\n", loggerProvider.Messages);
        Assert.DoesNotContain(apiSecret, logs);
        Assert.DoesNotContain(email, logs);
        Assert.DoesNotContain(reference, logs);
        Assert.DoesNotContain(callback, logs);
        Assert.DoesNotContain(accountNumber, logs);
        Assert.DoesNotContain("Sensitive Account Name", logs);
        Assert.DoesNotContain("authorization_url", logs);
    }

    private sealed class CapturingHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Clean-Connect.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found for source security checks.");
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => messages.Add(formatter(state, exception));
    }
}
