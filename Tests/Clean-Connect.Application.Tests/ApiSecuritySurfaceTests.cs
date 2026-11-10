using System.Reflection;
using Clean_Connect.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Clean_Connect.Application.Tests;

public sealed class ApiSecuritySurfaceTests
{
    private static readonly Assembly ApiAssembly = typeof(PaymentController).Assembly;

    [Fact]
    public void TestControllerAndSeedAndPayRoute_AreAbsentRegardlessOfEnvironmentConfiguration()
    {
        var productionConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["TestEndpoints:EnableSeedAndPay"] = "true"
            })
            .Build();

        Assert.Equal("Production", productionConfiguration["ASPNETCORE_ENVIRONMENT"]);
        Assert.Null(ApiAssembly.GetType("Clean_Connect.Api.Controllers.TestController"));

        var seedAndPayRoutes = ApiAssembly.DefinedTypes
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Select(method => method.GetCustomAttribute<HttpMethodAttribute>()?.Template)
            .Where(template => template is not null)
            .Where(template => template!.Contains("seed-and-pay", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(seedAndPayRoutes);
    }

    [Fact]
    public void PaymentController_RemainsAuthenticatedAndKeepsLegitimatePaymentActions()
    {
        var controller = typeof(PaymentController);

        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(controller.GetMethod(nameof(PaymentController.Initialize)));
        Assert.NotNull(controller.GetMethod(nameof(PaymentController.PayForAcceptedBooking)));

        var webhook = controller.GetMethod(nameof(PaymentController.PaystackWebhook));
        Assert.NotNull(webhook);
        Assert.NotNull(webhook!.GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
