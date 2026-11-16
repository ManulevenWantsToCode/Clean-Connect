using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Clean_Connect.Application.Behaviours;
using Clean_Connect.Infrastructure.Operational;
using Clean_Connect.Domain.Entities;
using Microsoft.AspNetCore.Http;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace Clean_Connect.Application.Tests;

public sealed class OperationalHardeningTests
{
    [Fact]
    public async Task Unexpected_exception_returns_safe_problem_details_with_correlation_id()
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.HeaderName] = "safe-correlation";
        context.TraceIdentifier = "safe-correlation";
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context,
            new InvalidOperationException("password=secret; SQL connection failed"), TestContext.Current.CancellationToken);
        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("safe-correlation", response.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("secret", response.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection failed", response.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("bad request", 400)]
    [InlineData("forbidden", 403)]
    [InlineData("missing", 404)]
    [InlineData("booking conflict", 409)]
    public async Task Known_request_failures_map_to_standard_status_codes(string kind, int expectedStatus)
    {
        Exception exception = kind switch
        {
            "bad request" => new ArgumentException("private detail"),
            "forbidden" => new UnauthorizedAccessException("private detail"),
            "missing" => new KeyNotFoundException("private detail"),
            _ => new Clean_Connect.Application.Interface.Repositories.BookingConflictException("private detail")
        };
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken));
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.DoesNotContain("private detail", Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
    }

    [Fact]
    public async Task Correlation_middleware_accepts_safe_ids_and_replaces_untrusted_values()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "client-request_123";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, NullLogger<CorrelationIdMiddleware>.Instance);
        await context.Response.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal("client-request_123", context.Response.Headers[CorrelationIdMiddleware.HeaderName]);

        var invalidContext = new DefaultHttpContext();
        invalidContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = "attacker\r\nX-Evil: yes";
        await middleware.InvokeAsync(invalidContext, NullLogger<CorrelationIdMiddleware>.Instance);
        Assert.True(CorrelationIdMiddleware.IsSafe(invalidContext.TraceIdentifier));
        Assert.NotEqual("attacker\r\nX-Evil: yes", invalidContext.TraceIdentifier);
    }

    [Fact]
    public void Rate_limit_partition_uses_validated_claim_or_remote_ip_not_identity_headers()
    {
        var anonymous = new DefaultHttpContext();
        anonymous.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.10");
        anonymous.Request.Headers["X-User-Id"] = "victim";
        Assert.Equal("ip:192.0.2.10", RateLimitConfiguration.GetPartitionKey(anonymous));

        var authenticated = new DefaultHttpContext();
        authenticated.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "identity-123")], "test"));
        authenticated.Request.Headers["X-User-Id"] = "victim";
        Assert.Equal("user:identity-123", RateLimitConfiguration.GetPartitionKey(authenticated));
    }

    [Fact]
    public async Task Fixed_window_rate_limit_enforces_partition_and_allows_other_authenticated_users()
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(
            context => RateLimitConfiguration.CreatePartition(context, permitLimit: 1, windowSeconds: 60));
        var first = new DefaultHttpContext();
        first.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.25");
        var sameIp = new DefaultHttpContext();
        sameIp.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.25");
        var otherUser = new DefaultHttpContext();
        otherUser.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "another-user")], "test"));

        using var firstLease = await limiter.AcquireAsync(first, 1, TestContext.Current.CancellationToken);
        using var rejectedLease = await limiter.AcquireAsync(sameIp, 1, TestContext.Current.CancellationToken);
        using var separateLease = await limiter.AcquireAsync(otherUser, 1, TestContext.Current.CancellationToken);
        Assert.True(firstLease.IsAcquired);
        Assert.False(rejectedLease.IsAcquired);
        Assert.True(separateLease.IsAcquired);
    }

    [Fact]
    public void Rate_limit_values_bind_from_environment_configuration()
    {
        const string permitKey = "RateLimiting__Authentication__PermitLimit";
        const string windowKey = "RateLimiting__Authentication__WindowSeconds";
        var priorPermit = Environment.GetEnvironmentVariable(permitKey);
        var priorWindow = Environment.GetEnvironmentVariable(windowKey);
        try
        {
            Environment.SetEnvironmentVariable(permitKey, "7");
            Environment.SetEnvironmentVariable(windowKey, "90");
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var settings = RateLimitConfiguration.ReadSettings(configuration, "RateLimiting:Authentication", 10, 60);
            Assert.Equal(7, settings.PermitLimit);
            Assert.Equal(90, settings.WindowSeconds);
        }
        finally
        {
            Environment.SetEnvironmentVariable(permitKey, priorPermit);
            Environment.SetEnvironmentVariable(windowKey, priorWindow);
        }
    }

    [Fact]
    public void Forwarded_headers_trust_only_explicit_configured_proxies_and_bounded_hops()
    {
        const string proxyKey = "ForwardedHeaders__KnownProxies__0";
        const string limitKey = "ForwardedHeaders__ForwardLimit";
        var priorProxy = Environment.GetEnvironmentVariable(proxyKey);
        var priorLimit = Environment.GetEnvironmentVariable(limitKey);
        try
        {
            Environment.SetEnvironmentVariable(proxyKey, "203.0.113.45");
            Environment.SetEnvironmentVariable(limitKey, "2");
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            using var provider = new ServiceCollection()
                .AddTrustedForwardedHeaders(configuration)
                .BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

            Assert.Contains(System.Net.IPAddress.Parse("203.0.113.45"), options.KnownProxies);
            Assert.Equal(2, options.ForwardLimit);
            Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        }
        finally
        {
            Environment.SetEnvironmentVariable(proxyKey, priorProxy);
            Environment.SetEnvironmentVariable(limitKey, priorLimit);
        }
    }

    [Fact]
    public async Task Health_response_contains_status_only()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await HealthResponseWriter.WriteAsync(context,
            new Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport(
                new Dictionary<string, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReportEntry>(),
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, TimeSpan.Zero));
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        Assert.Single(json.RootElement.EnumerateObject());
    }

    [Fact]
    public void Login_audit_event_contains_actor_and_safe_metadata_only()
    {
        var actorId = Guid.NewGuid();
        var audit = AuditRecord.CreateAuthenticationEvent(DateTime.UtcNow, actorId, succeeded: false, "correlation-7");
        Assert.Equal(actorId.ToString(), audit.ActorId);
        Assert.Equal("User", audit.ActorKind);
        Assert.Equal("LoginFailed", audit.Action);
        Assert.Equal("Authentication", audit.EntityType);
        Assert.Equal("[]", audit.ChangedFields);
        Assert.Equal("correlation-7", audit.CorrelationId);
        Assert.Equal("Anonymous", AuditRecord.CreateAuthenticationEvent(DateTime.UtcNow, null, false, null).ActorKind);
    }

    [Fact]
    public void Webhook_inbox_retains_only_safe_correlation_identifiers()
    {
        var received = Clean_Connect.Domain.Entities.PaystackWebhookInboxEvent.Receive(
            "event-key", "charge.success", "provider-id", "payment-reference", "{}", DateTime.UtcNow, "request_42");
        var unsafeId = Clean_Connect.Domain.Entities.PaystackWebhookInboxEvent.Receive(
            "another-key", "charge.success", null, null, "{}", DateTime.UtcNow, "bad\r\nvalue");
        Assert.Equal("request_42", received.CorrelationId);
        Assert.Null(unsafeId.CorrelationId);
    }
}
