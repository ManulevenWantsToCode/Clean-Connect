using AspNetCoreHero.ToastNotification.Abstractions;
using Clean_Connect.Application.Command.PaymentCommand;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Web.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Web.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _repo;
        private readonly INotyfService _notyf;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IMediator mediator,
            IUnitOfWork repo,
            INotyfService notyf,
            ILogger<PaymentController> logger)
        {
            _mediator = mediator;
            _repo = repo;
            _notyf = notyf;
            _logger = logger;
        }

        [HttpGet("Payment/Callback")]
        public async Task<IActionResult> Callback(string trxref, string reference, CancellationToken ct)
        {
            var client = await GetCurrentClientAsync(ct);
            if (client == null)
            {
                _notyf.Information("Please complete your client profile before confirming a payment.");
                return RedirectToAction("CreateClientProfile", "Client");
            }

            var paymentReference = reference ?? trxref;
            if (string.IsNullOrWhiteSpace(paymentReference))
            {
                _notyf.Error("We couldn't locate the payment reference.");
                return RedirectToAction("ClientResponses", "Booking");
            }

            try
            {
                var result = await _mediator.Send(new ConfirmPaymentCommand(paymentReference), ct);

                var booking = await _repo.Bookings.GetBookingById(result.BookingId, ct);
                if (booking == null || booking.ClientId != client.Id)
                {
                    _notyf.Error("The booking for this payment was not found for your profile.");
                    return RedirectToAction("ClientResponses", "Booking");
                }

                var model = new PaymentConfirmationViewModel
                {
                    Succeeded = result.Status.Equals("success", StringComparison.OrdinalIgnoreCase),
                    BookingId = result.BookingId,
                    ServiceName = booking.ServiceType?.Name ?? "Cleaning service",
                    WorkerName = booking.Worker?.FullName ?? "Your worker",
                    Amount = result.Amount,
                    PaymentReference = result.PaymentReference,
                    TransactionId = result.TransactionId,
                    FailureReason = result.FailureReason
                };

                model.Title = model.Succeeded
                    ? "Payment confirmed"
                    : "Payment not completed";

                model.Message = model.Succeeded
                    ? $"Your payment for {model.ServiceName} has been confirmed. {model.WorkerName} has been notified and the job is now scheduled."
                    : "Your payment did not go through. You can try again or choose another worker.";

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment confirmation failed for reference {Reference}", paymentReference);
                _notyf.Error(ex.Message);

                var model = new PaymentConfirmationViewModel
                {
                    Succeeded = false,
                    Title = "Payment not completed",
                    Message = "We couldn't confirm your payment right now. You can try again from your booking.",
                    BookingId = Guid.Empty
                };
                return View(model);
            }
        }

        private async Task<Client?> GetCurrentClientAsync(CancellationToken ct)
        {
            var email = User.Identity?.Name;
            return string.IsNullOrWhiteSpace(email) ? null : await _repo.Clients.GetByEmail(email, ct);
        }
    }
}