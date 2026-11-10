using AspNetCoreHero.ToastNotification.Abstractions;
using Clean_Connect.Application.Command.ApplicationUserCommand;
using Clean_Connect.Application.Command.Auth;
using Clean_Connect.Application.DTO;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Web.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace Clean_Connect.Application.Tests;

/// <summary>
/// Isolated MVC action-flow checks. The mediator and notification services are local doubles;
/// no database, email provider, or Google endpoint is contacted.
/// </summary>
public sealed class AuthenticationFlowTests
{
    [Fact]
    public async Task Registration_SendsCommand_AndRedirectsToPendingConfirmation()
    {
        var (mediator, recorder) = CreateMediator(request => request is RegisterUserCommand
            ? Guid.NewGuid()
            : throw new InvalidOperationException($"Unexpected request {request.GetType().Name}"));
        var controller = CreateController(mediator);

        var result = await controller.Register(
            new RegisterUserCommand("test.user@example.test", "CleanConnect!2026", "CleanConnect!2026"),
            CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AuthController.PendingConfirmation), ((RedirectToActionResult)result).ActionName);
        Assert.IsType<RegisterUserCommand>(Assert.Single(recorder.Requests));
    }

    [Fact]
    public async Task EmailConfirmation_ValidToken_ShowsConfirmationPage()
    {
        var (mediator, recorder) = CreateMediator(_ => true);
        var controller = CreateController(mediator);

        var result = await controller.ConfirmEmail(Guid.NewGuid(), "local-confirmation-token", CancellationToken.None);

        Assert.Equal("ConfirmEmail", Assert.IsType<ViewResult>(result).ViewName);
        Assert.IsType<ConfirmEmailCommand>(Assert.Single(recorder.Requests));
    }

    [Fact]
    public async Task Login_ConfirmedClient_RedirectsToClientDashboard()
    {
        var (mediator, recorder) = CreateMediator(_ => new LoginResponse
        {
            IsSuccessful = true,
            Roles = ["Client"]
        });
        var controller = CreateController(mediator);

        var result = await controller.Login(new LoginDto
        {
            Email = "test.user@example.test",
            Password = "CleanConnect!2026"
        }, TestContext.Current.CancellationToken);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirect.ActionName);
        Assert.Equal("Client", redirect.ControllerName);
        Assert.IsType<LoginCommand>(Assert.Single(recorder.Requests));
    }

    [Fact]
    public async Task Login_UnconfirmedEmail_RedirectsToVerificationInstructions()
    {
        var (mediator, _) = CreateMediator(_ => new LoginResponse { EmailNotConfirmed = true });
        var controller = CreateController(mediator);

        var result = await controller.Login(new LoginDto
        {
            Email = "test.user@example.test",
            Password = "CleanConnect!2026"
        }, TestContext.Current.CancellationToken);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AuthController.EmailNotConfirmed), redirect.ActionName);
    }

    [Fact]
    public async Task GoogleSignIn_WithoutConfiguredProvider_RedirectsToPasswordLogin()
    {
        var (mediator, _) = CreateMediator(_ => throw new InvalidOperationException());
        var controller = CreateController(mediator,
            new ConfigurationBuilder().Build());

        var result = await controller.GoogleLogin();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AuthController.Login), redirect.ActionName);
    }

    [Fact]
    public async Task GoogleCallback_RemoteProviderError_ReturnsToLogin()
    {
        var (mediator, _) = CreateMediator(_ => throw new InvalidOperationException());
        var controller = CreateController(mediator);

        var result = await controller.GoogleCallback(remoteError: "simulated-provider-error");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AuthController.Login), redirect.ActionName);
    }

    [Fact]
    public async Task GoogleCallback_ExistingExternalAccount_SignsInAndRedirectsHome()
    {
        var signInManager = new GoogleSignInManagerDouble
        {
            ExternalInfo = new ExternalLoginInfo(
                new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()),
                "Google",
                "test-provider-key",
                "Google")
        };
        var (mediator, _) = CreateMediator(_ => throw new InvalidOperationException());
        var controller = CreateController(mediator, signInManager: signInManager);

        var result = await controller.GoogleCallback();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.True(signInManager.ExternalSignInCalled);
    }

    [Fact]
    public async Task PasswordRecovery_RequestAndReset_ReachSuccessRedirect()
    {
        var (mediator, recorder) = CreateMediator(request => request switch
        {
            ForgotPasswordCommand => true,
            ResetPasswordCommand => true,
            _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}")
        });
        var controller = CreateController(mediator);

        var forgotResult = await controller.ForgotPassword(new ForgotPasswordDto { Email = "test.user@example.test" });
        var resetResult = await controller.ResetPassword(new ResetPasswordDto
        {
            Email = "test.user@example.test",
            Token = "local-reset-token",
            Password = "NewCleanConnect!2026",
            ConfirmPassword = "NewCleanConnect!2026"
        });

        Assert.Equal(nameof(AuthController.ResetLinkSent), Assert.IsType<RedirectToActionResult>(forgotResult).ActionName);
        Assert.Equal(nameof(AuthController.PasswordResetSuccess), Assert.IsType<RedirectToActionResult>(resetResult).ActionName);
        Assert.Collection(recorder.Requests,
            request => Assert.IsType<ForgotPasswordCommand>(request),
            request => Assert.IsType<ResetPasswordCommand>(request));
    }

    [Fact]
    public async Task ForgotPassword_UsesCurrentSchemeAndPortForLoopbackResetLink()
    {
        var (mediator, recorder) = CreateMediator(_ => true);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://localhost:44357"
            })
            .Build();
        var controller = CreateController(mediator, configuration);
        controller.Request.Scheme = "http";
        controller.Request.Host = new HostString("localhost", 44357);

        var result = await controller.ForgotPassword(new ForgotPasswordDto { Email = "test.user@example.test" });

        Assert.Equal(nameof(AuthController.ResetLinkSent), Assert.IsType<RedirectToActionResult>(result).ActionName);
        var command = Assert.IsType<ForgotPasswordCommand>(Assert.Single(recorder.Requests));
        Assert.Equal("http://localhost:44357", command.BaseUrl);
    }

    [Fact]
    public async Task ForgotPasswordHandler_UsesRequestBaseUrlAndEncodesResetToken()
    {
        var userManager = new PasswordResetUserManagerDouble("reset token/+?");
        var user = ApplicationUser.Create("test.user@example.test");
        user.Id = Guid.NewGuid();
        userManager.User = user;
        var (mediator, recorder) = CreateMediator(request => request is EmailSenderCommand ? Unit.Value : null);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://localhost:44357"
            })
            .Build();
        var handler = new ForgotPasswordCommandValidator.ForgotPasswordCommandHandler(userManager, mediator, configuration);

        var result = await handler.Handle(
            new ForgotPasswordCommand(user.Email!, "http://localhost:44357"),
            TestContext.Current.CancellationToken);

        Assert.True(result);
        var email = Assert.IsType<EmailSenderCommand>(Assert.Single(recorder.Requests));
        Assert.Contains("http://localhost:44357/Reset-Password?", email.Body);
        Assert.Contains("token=reset%20token%2F%2B%3F", email.Body);
    }

    private static (IMediator Mediator, MediatorDouble Recorder) CreateMediator(Func<object, object?> handler)
    {
        var mediator = DispatchProxy.Create<IMediator, MediatorDouble>();
        var recorder = (MediatorDouble)(object)mediator;
        recorder.Handler = handler;
        return (mediator, recorder);
    }

    private static AuthController CreateController(
        IMediator mediator,
        IConfiguration? configuration = null,
        SignInManager<ApplicationUser>? signInManager = null)
    {
        var controller = new AuthController(
            NullLogger<AuthController>.Instance,
            mediator,
            DispatchProxy.Create<INotyfService, NoOpNotyf>(),
            user: null!,
            signInManager: signInManager!,
            configuration ?? new ConfigurationBuilder().Build());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
            ActionDescriptor = new ControllerActionDescriptor()
        };
        controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        controller.Url = DispatchProxy.Create<IUrlHelper, NoOpUrlHelper>();

        return controller;
    }

    public class MediatorDouble : DispatchProxy
    {
        public Func<object, object?> Handler { get; set; } = _ => null;
        public List<object> Requests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(ISender.Send) || args is null)
                throw new NotSupportedException($"Unexpected mediator call {targetMethod?.Name}.");

            var request = args.OfType<object>().FirstOrDefault(value => value is not CancellationToken)
                ?? throw new InvalidOperationException("Mediator request was missing.");
            Requests.Add(request);

            var responseType = targetMethod.GetGenericArguments().FirstOrDefault()
                ?? throw new InvalidOperationException("Mediator response type was missing.");
            var response = Handler(request);
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(responseType)
                .Invoke(null, [response]);
        }
    }

    public class NoOpNotyf : DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) => null;
    }

    public class NoOpUrlHelper : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IUrlHelper.IsLocalUrl) ? false : null;
    }

    private sealed class PasswordResetUserManagerDouble : UserManager<ApplicationUser>
    {
        private readonly string _token;

        public PasswordResetUserManagerDouble(string token)
            : base(new EmptyUserStore(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<UserManager<ApplicationUser>>.Instance)
        {
            _token = token;
        }

        public ApplicationUser? User { get; set; }
        public override Task<ApplicationUser?> FindByEmailAsync(string email) => Task.FromResult(User);
        public override Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user) => Task.FromResult(_token);

        private sealed class EmptyUserStore : IUserStore<ApplicationUser>
        {
            public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
            public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
            public void Dispose() { }
            public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
            public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
            public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
            public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
            public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
            public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
            public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
            public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        }
    }

    private sealed class GoogleSignInManagerDouble : SignInManager<ApplicationUser>
    {
        public GoogleSignInManagerDouble()
            : this(CreateUserManager())
        {
        }

        private GoogleSignInManagerDouble(UserManager<ApplicationUser> userManager)
            : base(userManager,
                new HttpContextAccessor(),
                new UserClaimsPrincipalFactory<ApplicationUser>(userManager,
                    Microsoft.Extensions.Options.Options.Create(new IdentityOptions())),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                NullLogger<SignInManager<ApplicationUser>>.Instance,
                new AuthenticationSchemeProvider(Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions())),
                new DefaultUserConfirmation<ApplicationUser>())
        {
        }

        public ExternalLoginInfo? ExternalInfo { get; init; }
        public bool ExternalSignInCalled { get; private set; }

        public override Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(string? expectedXsrf = null) =>
            Task.FromResult(ExternalInfo);

        public override Task<Microsoft.AspNetCore.Identity.SignInResult> ExternalLoginSignInAsync(
            string loginProvider,
            string providerKey,
            bool isPersistent,
            bool bypassTwoFactor = false)
        {
            ExternalSignInCalled = true;
            return Task.FromResult(Microsoft.AspNetCore.Identity.SignInResult.Success);
        }

        private static UserManager<ApplicationUser> CreateUserManager()
        {
            var options = Microsoft.Extensions.Options.Options.Create(new IdentityOptions());
            return new UserManager<ApplicationUser>(
                new EmptyUserStore(),
                options,
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<UserManager<ApplicationUser>>.Instance);
        }

        private sealed class EmptyUserStore : IUserStore<ApplicationUser>
        {
            public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
            public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
            public void Dispose() { }
            public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
            public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
            public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
            public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
            public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
            public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
            public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
            public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        }
    }
}
