using Clean_Connect.Application.Command.PaymentCommand;
using Clean_Connect.Application.Command.WebhookCommand;
using Clean_Connect.Application.DTO;
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
    public class PaymentController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly ILogger<PaymentController> logger;

        public PaymentController(IMediator mediator, ILogger<PaymentController> logger)
        {
            this.mediator = mediator;
            this.logger = logger;
        }

        [HttpGet("payments/{paymentId:guid}/get-payment-by-id")]
        public async Task<IActionResult> GetPaymentById(Guid paymentId, CancellationToken cancellationToken)
        {
            logger.LogInformation("Fetching payment for PaymentID: {PaymentID}", paymentId);

            var payment = new GetPaymentByIdCQuery(paymentId);

            var result = await mediator.Send(payment, cancellationToken).ConfigureAwait(false);

            return Ok(result);
        }

        [HttpGet("Get-all-Payments")]
        public async Task<IActionResult> GetAllPayments(GetAllPaymentsQuery command, CancellationToken cancellationToken)
        {
            logger.LogInformation("Get all payments operation started");
            var result = await mediator.Send(command, cancellationToken);

            return StatusCode(200, result);
        }

        [HttpGet("Get-Payment-By-Reference")]
        public async Task<IActionResult> GetPaymentByReference(string reference, CancellationToken cancellationToken)
        {
            logger.LogInformation("Fetching payment for Reference: {Reference}", reference);
            var payment = new GetPaymentByReferenceQuery(reference);
            var result = await mediator.Send(payment, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }

        [HttpGet("Get-Payments-By-Payment-Status")]
        public async Task<IActionResult> GetPaymentsByPaymentStatus(PaymentStatus paymentStatus, CancellationToken cancellationToken)
        {
            logger.LogInformation("Fetching payments for PaymentStatus: {PaymentStatus}", paymentStatus);
            var payment = new GetByPaymentStatusQuery(paymentStatus);
            var result = await mediator.Send(payment, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }

        [HttpGet("Get-Payments-By-BookingId")]
        public async Task<IActionResult> GetPaymentsByBookingId(Guid bookingId, CancellationToken cancellationToken)
        {
            logger.LogInformation("Fetching payments for BookingId: {BookingId}", bookingId);
            var query = new GetPaymentByBookingIdQuery(bookingId);
            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }

       

        [HttpPost("initialize")]
        public async Task<IActionResult> Initialize([FromBody] InitializePaymentCommand command, CancellationToken cancellationToken)
        {
            var url = await mediator.Send(command, cancellationToken);
            logger.LogInformation(
                "Payment initialized for BookingId: {BookingId}, Amount: {Amount}, Email: {Email}, PaymentMethod: {PaymentMethod}",
                command.BookingId,
                command.Amount,
                command.Email,
                command.PaymentMethod);

            return Ok(new { checkoutUrl = url });
        }

        [HttpPost("bookings/{bookingId:guid}/pay")]
        public async Task<IActionResult> PayForAcceptedBooking(Guid bookingId, [FromBody] ClientPaymentRequest request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Payment initialization for accepted booking {BookingId} by client {ClientId}", bookingId, request.ClientId);
            var command = new PayForAcceptedBookingCommand(
                bookingId,
                request.ClientId,
                request.Email,
                request.PaymentMethod,
                request.CreatedBy);

            var result = await mediator.Send(command, cancellationToken);

            logger.LogInformation(
                "Payment initialized for accepted booking {BookingId} by client {ClientId}",
                bookingId,
                request.ClientId);

            return Ok(result);
        }

        [HttpDelete("payments/{paymentId:guid}")]
        public async Task<IActionResult> DeletePayment(Guid paymentId, CancellationToken cancellationToken)
        {
            logger.LogInformation("Deleting payment with ID: {PaymentId}", paymentId);
            var command = new DeletePaymentCommand(paymentId);

            var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Payment with ID {PaymentId} deleted successfully", paymentId);
            return Ok(new { success = result, message = "Payment deleted successfully" });
        }

        [HttpPost("bookings/{bookingId:guid}/payout")]
        public async Task<IActionResult> RequestPayout(Guid bookingId, [FromBody] PayoutRequest request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Payout requested for booking {BookingId} by worker {WorkerId}", bookingId, request.WorkerId);

            var command = new RequestPayoutCommand(
                bookingId,
                request.WorkerId,
                request.ModifiedBy);

            var result = await mediator.Send(command, cancellationToken);

            return Ok(result);
        }

        [HttpPost("paystack/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PaystackWebhook(CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(Request.Body);
            logger.LogInformation("Received Paystack webhook with headers: {Headers}", Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));

            var payload = await reader.ReadToEndAsync(cancellationToken);
            logger.LogInformation("Webhook payload: {Payload}", payload);

            var signature = Request.Headers["x-paystack-signature"].ToString();
            logger.LogInformation("Received Paystack webhook with signature: {Signature}", signature);
            if (string.IsNullOrEmpty(signature))
            {
                logger.LogWarning("Missing signature header in Paystack webhook");
                return BadRequest("Missing signature header.");
            }

            var command = new ProcessPaystackWebhookCommand(payload, signature);

            await mediator.Send(command, cancellationToken);

            return Ok();
        }
    }
}
