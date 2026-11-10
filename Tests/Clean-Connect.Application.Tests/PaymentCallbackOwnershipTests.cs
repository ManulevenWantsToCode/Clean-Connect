using AspNetCoreHero.ToastNotification.Abstractions;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Domain.Enums;
using Clean_Connect.Domain.Value_Objects;
using Clean_Connect.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace Clean_Connect.Application.Tests;

public sealed class PaymentCallbackOwnershipTests
{
    [Fact]
    public async Task Callback_DoesNotVerifyAnotherClientsPayment()
    {
        var client = CreateClient("signed-in@example.test");
        var booking = CreateBooking(Guid.NewGuid());
        var payment = Payment.Create(booking.Id, 1000m, "another-client-reference", "Card", "", DateTime.UtcNow);
        var (controller, mediator) = CreateController(client, booking, payment, _ =>
            throw new InvalidOperationException("A foreign payment must not be verified."));

        var result = await controller.Callback(payment.PaymentReference, payment.PaymentReference, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(mediator.Requests);
    }

    [Fact]
    public async Task Callback_StillVerifiesPaymentForOwningClient()
    {
        var client = CreateClient("signed-in@example.test");
        var booking = CreateBooking(client.Id);
        var payment = Payment.Create(booking.Id, 1000m, "owned-payment-reference", "Card", "", DateTime.UtcNow);
        var confirmation = new PaymentConfirmationResult(
            booking.Id,
            payment.Id,
            payment.Amount,
            payment.PaymentReference,
            "transaction-id",
            "success",
            null);
        var (controller, mediator) = CreateController(client, booking, payment, _ => confirmation);

        var result = await controller.Callback(payment.PaymentReference, payment.PaymentReference, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Single(mediator.Requests);
    }

    private static (PaymentController Controller, AuthenticationFlowTests.MediatorDouble Mediator) CreateController(
        Client client,
        Booking booking,
        Payment payment,
        Func<object, object?> mediatorHandler)
    {
        var clients = ApiOwnershipAuthorizationTests.InterfaceStub<IClientRepository>.Create((method, _) =>
            method.Name == nameof(IClientRepository.GetByEmail) ? Task.FromResult(client) : null);
        var bookings = ApiOwnershipAuthorizationTests.InterfaceStub<IBookingRepository>.Create((method, _) =>
            method.Name == nameof(IBookingRepository.GetBookingById) ? Task.FromResult(booking) : null);
        var payments = ApiOwnershipAuthorizationTests.InterfaceStub<IPaymentRepository>.Create((method, _) =>
            method.Name == nameof(IPaymentRepository.GetByReferenceAsync) ? Task.FromResult<Payment?>(payment) : null);
        var unitOfWork = ApiOwnershipAuthorizationTests.InterfaceStub<IUnitOfWork>.Create((method, _) => method.Name switch
        {
            "get_Clients" => clients,
            "get_Bookings" => bookings,
            "get_Payments" => payments,
            _ => null
        });

        var mediator = DispatchProxy.Create<MediatR.IMediator, AuthenticationFlowTests.MediatorDouble>();
        var mediatorDouble = (AuthenticationFlowTests.MediatorDouble)(object)mediator;
        mediatorDouble.Handler = mediatorHandler;

        var controller = new PaymentController(
            mediator,
            unitOfWork,
            DispatchProxy.Create<INotyfService, AuthenticationFlowTests.NoOpNotyf>(),
            NullLogger<PaymentController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, client.Email.ToString()) },
                    authenticationType: "test"))
            }
        };

        return (controller, mediatorDouble);
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
        "PAYTESTCLIENT");

    private static Booking CreateBooking(Guid clientId) => Booking.Create(
        clientId,
        Guid.NewGuid(),
        Location.Create(6.5244, 3.3792),
        DateTime.UtcNow.AddDays(1),
        DateTime.UtcNow,
        DateTime.UtcNow.AddDays(1).AddHours(9),
        DateTime.UtcNow.AddDays(1).AddHours(12),
        1000m,
        3000m,
        1000m,
        3000m,
        TimeRange.Morning,
        Address.Create("1 Test Street, Lagos"),
        BookingStatus.AcceptedAwaitingPayment,
        PaymentStatus.Pending,
        Guid.NewGuid());
}
