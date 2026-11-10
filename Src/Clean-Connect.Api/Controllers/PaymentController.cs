using Clean_Connect.Application.Command.PaymentCommand;
using Clean_Connect.Application.Command.WebhookCommand;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.Query.PaymentQuery;
using Clean_Connect.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class PaymentController : ApiControllerBase
    {
        private readonly IMediator mediator;
        private readonly ILogger<PaymentController> logger;

        public PaymentController(IMediator mediator, ILogger<PaymentController> logger, IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
            this.mediator = mediator;
            this.logger = logger;
        }

        [HttpGet("payments/{paymentId:guid}/get-payment-by-id")]
        [Authorize(Roles = "Client,Worker,Admin")]
        public async Task<IActionResult> GetPaymentById(Guid paymentId, CancellationToken cancellationToken)
        {
            var payment = await UnitOfWork.Payments.GetPaymentById(paymentId, cancellationToken);
            if (!await CanAccessPaymentAsync(payment, cancellationToken))
                return NotFound();

            var result = await mediator.Send(new GetPaymentByIdCQuery(paymentId), cancellationToken);
            return Ok(result);
        }

        [HttpGet("Get-all-Payments")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllPayments(GetAllPaymentsQuery command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("Get-Payment-By-Reference")]
        [Authorize(Roles = "Client,Worker,Admin")]
        public async Task<IActionResult> GetPaymentByReference(string reference, CancellationToken cancellationToken)
        {
            var payment = await UnitOfWork.Payments.GetByReferenceAsync(reference, cancellationToken);
            if (!await CanAccessPaymentAsync(payment, cancellationToken))
                return NotFound();

            var result = await mediator.Send(new GetPaymentByReferenceQuery(reference), cancellationToken);
            return Ok(result);
        }

        [HttpGet("Get-Payments-By-Payment-Status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPaymentsByPaymentStatus(PaymentStatus paymentStatus, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetByPaymentStatusQuery(paymentStatus), cancellationToken);
            return Ok(result);
        }

        [HttpGet("Get-Payments-By-BookingId")]
        [Authorize(Roles = "Client,Worker,Admin")]
        public async Task<IActionResult> GetPaymentsByBookingId(Guid bookingId, CancellationToken cancellationToken)
        {
            if (!await CanAccessBookingAsync(bookingId, cancellationToken))
                return NotFound();

            var result = await mediator.Send(new GetPaymentByBookingIdQuery(bookingId), cancellationToken);
            return Ok(result);
        }

        [HttpPost("initialize")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Initialize([FromBody] InitializePaymentCommand command, CancellationToken cancellationToken)
        {
            var client = await GetCurrentClientAsync(cancellationToken);
            var booking = await UnitOfWork.Bookings.GetBookingById(command.BookingId, cancellationToken);
            if (client is null || booking is null || booking.ClientId != client.Id)
                return NotFound();

            command = command with
            {
                Amount = booking.Amount,
                Email = CurrentUserEmail!,
                TransactionId = null,
                CreatedBy = CurrentUserId
            };

            var url = await mediator.Send(command, cancellationToken);
            return Ok(new { checkoutUrl = url });
        }

        [HttpPost("bookings/{bookingId:guid}/pay")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> PayForAcceptedBooking(Guid bookingId, [FromBody] ClientPaymentRequest request, CancellationToken cancellationToken)
        {
            var client = await GetCurrentClientAsync(cancellationToken);
            var booking = await UnitOfWork.Bookings.GetBookingById(bookingId, cancellationToken);
            if (client is null || booking is null || booking.ClientId != client.Id)
                return NotFound();

            var command = new PayForAcceptedBookingCommand(
                bookingId,
                client.Id,
                CurrentUserEmail!,
                request.PaymentMethod,
                CurrentUserId);

            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpDelete("payments/{paymentId:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePayment(Guid paymentId, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new DeletePaymentCommand(paymentId, CurrentUserId), cancellationToken);
            return Ok(new { success = result, message = "Payment deleted successfully" });
        }

        [HttpPost("bookings/{bookingId:guid}/payout")]
        [Authorize(Roles = "Worker")]
        public async Task<IActionResult> RequestPayout(Guid bookingId, [FromBody] PayoutRequest request, CancellationToken cancellationToken)
        {
            var worker = await GetCurrentWorkerAsync(cancellationToken);
            var booking = await UnitOfWork.Bookings.GetBookingById(bookingId, cancellationToken);
            if (worker is null || booking is null || booking.WorkerId != worker.Id)
                return NotFound();

            var command = new RequestPayoutCommand(bookingId, worker.Id, CurrentUserId);
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("paystack/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PaystackWebhook(CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync(cancellationToken);
            var signature = Request.Headers["x-paystack-signature"].ToString();
            if (string.IsNullOrEmpty(signature))
                return BadRequest("Missing signature header.");

            await mediator.Send(new ProcessPaystackWebhookCommand(payload, signature), cancellationToken);
            return Ok();
        }
    }
}
