using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Clean_Connect.Api.Security;
using Clean_Connect.Application.DTO;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Clean_Connect.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Clean_Connect.Api.Controllers;
using MediatR;
using Xunit;

namespace Clean_Connect.Application.Tests;

public sealed class RemainingSecurityRemediationTests
{
    [Fact]
    public void CorsPolicy_AllowsOnlyConfiguredOriginAndDoesNotEnableWildcardOrCredentials()
    {
        var policy = FrontendCorsPolicy.Build(["https://frontend.example:8443"]);
        Assert.Contains("https://frontend.example:8443", policy.Origins);
        Assert.False(policy.AllowAnyOrigin);
        Assert.False(policy.SupportsCredentials);
        Assert.DoesNotContain("https://untrusted.example", policy.Origins);
        Assert.Throws<InvalidOperationException>(() => FrontendCorsPolicy.Build(["*"]));
    }

    [Fact]
    public async Task JwtValidation_RejectsRevokedTokenAndIsScopedToItsUser()
    {
        var userId = Guid.NewGuid();
        var store = new TestRevocationStore((user, jti) => user == userId && jti == "revoked");
        var provider = new ServiceCollection().AddSingleton<IJwtRevocationStore>(store).BuildServiceProvider();
        var accepted = CreateTokenContext(provider, userId, "active");
        await JwtRevocationEvents.ValidateTokenAsync(accepted);
        Assert.Null(accepted.Result?.Failure);

        var revoked = CreateTokenContext(provider, userId, "revoked");
        await JwtRevocationEvents.ValidateTokenAsync(revoked);
        Assert.NotNull(revoked.Result?.Failure);

        var otherUser = CreateTokenContext(provider, Guid.NewGuid(), "revoked");
        await JwtRevocationEvents.ValidateTokenAsync(otherUser);
        Assert.Null(otherUser.Result?.Failure);
        Assert.Equal(3, store.Checks);
    }

    [Fact]
    public async Task Logout_RevokesOnlyTheAuthenticatedUsersCurrentJti()
    {
        var userId = Guid.NewGuid();
        var store = new CapturingRevocationStore();
        var mediator = DispatchProxy.Create<IMediator, LogoutMediatorProxy>();
        var controller = new AccountController(mediator, store)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(JwtRegisteredClaimNames.Jti, "session-jti"),
                        new Claim(JwtRegisteredClaimNames.Exp, DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString())
                    ], JwtBearerDefaults.AuthenticationScheme))
                }
            }
        };

        await controller.Logout();
        Assert.Equal(userId, store.UserId);
        Assert.Equal("session-jti", store.TokenId);
        Assert.True(store.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void JwtValidation_ContinuesToEnforceSignatureAndExpiry()
    {
        var key = new SymmetricSecurityKey(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var handler = new JwtSecurityTokenHandler();
        string Create(DateTime expires) => handler.WriteToken(new JwtSecurityToken("issuer", "audience", [], DateTime.UtcNow.AddMinutes(-2), expires, credentials));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "issuer",
            ValidateAudience = true,
            ValidAudience = "audience",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        Assert.NotNull(handler.ValidateToken(Create(DateTime.UtcNow.AddMinutes(1)), parameters, out _));
        Assert.Throws<SecurityTokenExpiredException>(() => handler.ValidateToken(Create(DateTime.UtcNow.AddMinutes(-1)), parameters, out _));
    }

    [Fact]
    public void RevocationsUseExistingPersistentIdentityTokenTable()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused", options => options.UseNetTopologySuite())
            .Options;
        using var db = new ApplicationDbContext(options);
        var tokenEntity = db.Model.FindEntityType(typeof(IdentityUserToken<Guid>));
        Assert.NotNull(tokenEntity);
        Assert.Equal("AspNetUserTokens", tokenEntity!.GetTableName());
        Assert.Equal(3, tokenEntity.FindPrimaryKey()!.Properties.Count);
    }

    [Fact]
    public void WorkerMarketplaceResponse_ContainsOnlyApprovedDiscoveryFields()
    {
        var names = typeof(WorkerMarketplaceDto).GetProperties().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(new[] { "WorkerId", "Name", "ServiceType", "State", "Rating", "HourlyRate" }.ToHashSet(StringComparer.Ordinal), names);
        Assert.DoesNotContain("Email", names);
        Assert.DoesNotContain("Address", names);
        Assert.DoesNotContain("Contact", names);
        Assert.DoesNotContain("Gender", names);
        Assert.DoesNotContain("DateOfBirth", names);
        Assert.DoesNotContain("BankAccount", names);
    }

    [Fact]
    public void WorkerSelfProfileEndpoint_IsRestrictedToWorkerRole()
    {
        var workerController = typeof(WorkerController);
        var method = workerController.GetMethod("GetMyProfile");
        var roleAuthorization = method!.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.Equal("Worker", roleAuthorization?.Roles);

        var allWorkersAuthorization = workerController.GetMethod("GetAllWorkers")!
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.Equal("Admin", allWorkersAuthorization?.Roles);

        var publicFields = typeof(WorkerMarketplaceDto).GetProperties().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("Email", publicFields);
        Assert.DoesNotContain("Contact", publicFields);
        Assert.DoesNotContain("Address", publicFields);
    }

    private static TokenValidatedContext CreateTokenContext(IServiceProvider services, Guid userId, string jti)
    {
        var http = new DefaultHttpContext { RequestServices = services };
        var context = new TokenValidatedContext(http,
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            new JwtBearerOptions());
        context.Principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        ], JwtBearerDefaults.AuthenticationScheme));
        return context;
    }

    private sealed class TestRevocationStore(Func<Guid, string, bool> revoked) : IJwtRevocationStore
    {
        public int Checks { get; private set; }
        public Task RevokeAsync(Guid userId, string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> IsRevokedAsync(Guid userId, string tokenId, CancellationToken cancellationToken)
        {
            Checks++;
            return Task.FromResult(revoked(userId, tokenId));
        }
    }

    private sealed class CapturingRevocationStore : IJwtRevocationStore
    {
        public Guid UserId { get; private set; }
        public string? TokenId { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }
        public Task RevokeAsync(Guid userId, string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken)
        {
            UserId = userId;
            TokenId = tokenId;
            ExpiresAt = expiresAt;
            return Task.CompletedTask;
        }
        public Task<bool> IsRevokedAsync(Guid userId, string tokenId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    public class LogoutMediatorProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Task.FromResult(Unit.Value);
    }
}
