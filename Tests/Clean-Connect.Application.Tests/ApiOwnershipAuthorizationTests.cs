using System.Reflection;
using System.Security.Claims;
using Clean_Connect.Api.Controllers;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Value_Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Clean_Connect.Application.Tests;

public sealed class ApiOwnershipAuthorizationTests
{
    [Fact]
    public async Task ClientCannotAccessAnotherClientsBooking()
    {
        var client = CreateClient("client-a@example.test");
        var booking = CreateBooking(clientId: Guid.NewGuid(), workerId: Guid.NewGuid());
        var controller = CreateOwnershipProbe(client, booking, worker: null, payment: null, "Client");

        Assert.False(await controller.CanAccessBooking(booking.Id));
    }

    [Fact]
    public async Task WorkerCannotAccessAnotherWorkersPayoutPayment()
    {
        var worker = CreateWorker("worker-a@example.test");
        var booking = CreateBooking(clientId: Guid.NewGuid(), workerId: Guid.NewGuid());
        var payment = Payment.Create(booking.Id, 1000m, "reference", "Card", "", DateTime.UtcNow);
        var controller = CreateOwnershipProbe(client: null, booking, worker, payment, "Worker");

        Assert.False(await controller.CanAccessPayment(payment));
    }

    [Fact]
    public async Task ClientAndWorkerCanAccessTheirOwnBookingsAndPayments()
    {
        var client = CreateClient("client-owner@example.test");
        var clientBooking = CreateBooking(client.Id, Guid.NewGuid());
        var clientController = CreateOwnershipProbe(client, clientBooking, worker: null, payment: null, "Client");
        Assert.True(await clientController.CanAccessBooking(clientBooking.Id));

        var worker = CreateWorker("worker-owner@example.test");
        var workerBooking = CreateBooking(Guid.NewGuid(), worker.Id);
        var workerPayment = Payment.Create(workerBooking.Id, 1000m, "owner-reference", "Card", "", DateTime.UtcNow);
        var workerController = CreateOwnershipProbe(client: null, workerBooking, worker, workerPayment, "Worker");
        Assert.True(await workerController.CanAccessPayment(workerPayment));
    }

    [Fact]
    public void ClientAndWorkerActionsAreRoleSeparatedAndAdminActionsAreRestricted()
    {
        AssertRole<ClientController>(nameof(ClientController.MarkBookingAsCompleted), "Client");
        AssertRole<WorkerController>(nameof(WorkerController.AcceptBooking), "Worker");
        AssertRole<WorkerController>(nameof(WorkerController.RejectBooking), "Worker");
        AssertRole<WorkerController>(nameof(WorkerController.UpdateWorker), "Worker");
        AssertRole<ServiceTypeController>(nameof(ServiceTypeController.CreateServiceType), "Admin");
        AssertRole<Clean_Connect.Web.Controllers.ServiceTypeController>(nameof(Clean_Connect.Web.Controllers.ServiceTypeController.CreateServiceType), "Admin");
        AssertRole<CouponController>(nameof(CouponController.CreateCoupon), "Admin");
        AssertRole<PaymentController>(nameof(PaymentController.DeletePayment), "Admin");
        AssertRole<PaymentController>(nameof(PaymentController.GetAllPayments), "Admin");
    }

    [Fact]
    public void RegistrationAndLoginRemainAnonymousAndProtectedActionsRequireAuthentication()
    {
        AssertAnonymous<AccountController>(nameof(AccountController.Register));
        AssertAnonymous<AccountController>(nameof(AccountController.Login));
        AssertAnonymous<AccountController>(nameof(AccountController.ConfirmEmail));
        AssertAuthenticated<AccountController>(nameof(AccountController.Logout));

        AssertRole<ClientController>(nameof(ClientController.RegisterClient), "Client");
        AssertRole<BookingController>(nameof(BookingController.CreateBooking), "Client");
        AssertRole<PaymentController>(nameof(PaymentController.Initialize), "Client");
        AssertRole<PaymentController>(nameof(PaymentController.RequestPayout), "Worker");
    }

    [Fact]
    public void OnlyApprovedReferenceAndWebhookRoutesRemainAnonymous()
    {
        AssertAnonymous<ServiceTypeController>(nameof(ServiceTypeController.GetAllServiceTypes));
        AssertAnonymous<ServiceTypeController>(nameof(ServiceTypeController.GetServiceTypeById));
        AssertAnonymous<CouponController>(nameof(CouponController.GetCouponByCode));
        AssertAnonymous<CouponController>(nameof(CouponController.ValidateCoupon));
        AssertAnonymous<PaymentController>(nameof(PaymentController.PaystackWebhook));
    }

    private static OwnershipProbeController CreateOwnershipProbe(
        Client? client,
        Booking booking,
        Worker? worker,
        Payment? payment,
        string role)
    {
        var clients = InterfaceStub<IClientRepository>.Create((method, _) =>
            method.Name == nameof(IClientRepository.GetByEmail) ? Task.FromResult(client!) : null);
        var workers = InterfaceStub<IWorkerRepository>.Create((method, _) =>
            method.Name == nameof(IWorkerRepository.GetByEmail) ? Task.FromResult(worker!) : null);
        var bookings = InterfaceStub<IBookingRepository>.Create((method, _) =>
            method.Name == nameof(IBookingRepository.GetBookingById) ? Task.FromResult(booking) : null);
        var payments = InterfaceStub<IPaymentRepository>.Create((method, _) =>
            method.Name == nameof(IPaymentRepository.GetPaymentById) ? Task.FromResult(payment!) : null);

        var unitOfWork = InterfaceStub<IUnitOfWork>.Create((method, _) => method.Name switch
        {
            "get_Clients" => clients,
            "get_Workers" => workers,
            "get_Bookings" => bookings,
            "get_Payments" => payments,
            _ => null
        });

        var controller = new OwnershipProbeController(unitOfWork);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Email, client?.Email.ToString() ?? worker?.Email.ToString() ?? "missing@example.test"),
            new Claim(ClaimTypes.Role, role)
        }, authenticationType: "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static Client CreateClient(string email) => Client.Create(
        FullName.Create("Casey", "Client"),
        Address.Create("1 Test Street, Lagos"),
        Email.Create(email),
        Location.Create(6.5244, 3.3792),
        Gender.Male,
        PhoneNumber.Create("08012345678"),
        "Lagos",
        new DateTime(1990, 1, 1),
        "TESTCLIENT");

    private static Worker CreateWorker(string email) => Worker.Create(
        FullName.Create("Wes", "Worker"),
        Address.Create("2 Test Street, Lagos"),
        PhoneNumber.Create("08012345679"),
        Location.Create(6.5244, 3.3792),
        Gender.Male,
        Guid.NewGuid(),
        Email.Create(email),
        "Lagos",
        new DateTime(1990, 1, 1),
        2000m);

    private static Booking CreateBooking(Guid clientId, Guid workerId) => Booking.Create(
        clientId,
        workerId,
        Location.Create(6.5244, 3.3792),
        DateTime.UtcNow.AddDays(1),
        DateTime.UtcNow,
        DateTime.UtcNow.AddDays(1).AddHours(9),
        DateTime.UtcNow.AddDays(1).AddHours(12),
        2000m,
        6000m,
        6000m,
        6000m,
        TimeRange.Morning,
        Address.Create("1 Test Street, Lagos"),
        BookingStatus.AcceptedAwaitingPayment,
        PaymentStatus.Pending,
        Guid.NewGuid());

    private static void AssertRole<TController>(string methodName, string expectedRole)
    {
        var methods = typeof(TController).GetMethods().Where(method => method.Name == methodName).ToArray();
        Assert.NotEmpty(methods);
        var controllerRoles = typeof(TController).GetCustomAttribute<AuthorizeAttribute>()?.Roles;
        Assert.Contains(methods, method =>
            method.GetCustomAttribute<AuthorizeAttribute>()?.Roles == expectedRole || controllerRoles == expectedRole);
    }

    private static void AssertAnonymous<TController>(string methodName)
    {
        var methods = typeof(TController).GetMethods().Where(method => method.Name == methodName).ToArray();
        Assert.NotEmpty(methods);
        Assert.Contains(methods, method => method.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
    }

    private static void AssertAuthenticated<TController>(string methodName)
    {
        var methods = typeof(TController).GetMethods().Where(method => method.Name == methodName).ToArray();
        Assert.NotEmpty(methods);
        Assert.Contains(methods, method => method.GetCustomAttribute<AuthorizeAttribute>() is not null);
    }

    private sealed class OwnershipProbeController(IUnitOfWork unitOfWork) : ApiControllerBase(unitOfWork)
    {
        public Task<bool> CanAccessBooking(Guid bookingId) => CanAccessBookingAsync(bookingId, CancellationToken.None);
        public Task<bool> CanAccessPayment(Payment payment) => CanAccessPaymentAsync(payment, CancellationToken.None);
    }

    public class InterfaceStub<T> : DispatchProxy where T : class
    {
        private Func<MethodInfo, object?[]?, object?> handler = null!;

        public static T Create(Func<MethodInfo, object?[]?, object?> handler)
        {
            var proxy = DispatchProxy.Create<T, InterfaceStub<T>>();
            ((InterfaceStub<T>)(object)proxy).handler = handler;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return handler(targetMethod!, args);
        }
    }
}
