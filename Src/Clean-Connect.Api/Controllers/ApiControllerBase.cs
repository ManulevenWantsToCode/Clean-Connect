using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Clean_Connect.Api.Controllers;

public abstract class ApiControllerBase(IUnitOfWork unitOfWork) : ControllerBase
{
    protected IUnitOfWork UnitOfWork { get; } = unitOfWork;

    protected bool IsAdministrator => User.IsInRole("Admin");

    protected string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    protected string? CurrentUserEmail =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(ClaimTypes.Name);

    protected async Task<Client?> GetCurrentClientAsync(CancellationToken cancellationToken)
    {
        var email = CurrentUserEmail;
        return string.IsNullOrWhiteSpace(email)
            ? null
            : await UnitOfWork.Clients.GetByEmail(email, cancellationToken);
    }

    protected async Task<Worker?> GetCurrentWorkerAsync(CancellationToken cancellationToken)
    {
        var email = CurrentUserEmail;
        return string.IsNullOrWhiteSpace(email)
            ? null
            : await UnitOfWork.Workers.GetByEmail(email, cancellationToken);
    }

    protected async Task<bool> CanAccessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        if (IsAdministrator)
            return true;

        var booking = await UnitOfWork.Bookings.GetBookingById(bookingId, cancellationToken);
        if (booking is null)
            return false;

        if (User.IsInRole("Client"))
        {
            var client = await GetCurrentClientAsync(cancellationToken);
            return client is not null && booking.ClientId == client.Id;
        }

        if (User.IsInRole("Worker"))
        {
            var worker = await GetCurrentWorkerAsync(cancellationToken);
            return worker is not null && booking.WorkerId == worker.Id;
        }

        return false;
    }

    protected async Task<bool> CanAccessPaymentAsync(Payment? payment, CancellationToken cancellationToken)
    {
        return payment is not null && await CanAccessBookingAsync(payment.BookingId, cancellationToken);
    }
}
