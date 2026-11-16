using Clean_Connect.Application.Command.PaymentCommand;
using Clean_Connect.Application.Command.WebhookCommand;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.DTO.Pagination;
using Clean_Connect.Application.Query.PaymentQuery;
using Clean_Connect.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Clean_Connect.Infrastructure.Operational;

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
        private readonly IPaystackWebhookInboxRepository webhookInbox;
        private readonly IPagedReadRepository pagedReads;

        public PaymentController(IMediator mediator, ILogger<PaymentController> logger, IUnitOfWork unitOfWork, IPaystackWebhookInboxRepository webhookInbox, IPagedReadRepository pagedReads)
            : base(unitOfWork)
        {
            this.mediator = mediator;
            this.logger = logger;
            this.webhookInbox = webhookInbox;
            this.pagedReads = pagedReads;
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
        public async Task<IActionResult> GetAllPayments([FromQuery] PaymentPageRequest page, CancellationToken cancellationToken)
        {
            if (!HasValidDateRange(page)) return BadRequest(new { message = "FromDate must be earlier than or equal to ToDate." });
            var result = await pagedReads.GetPaymentsAsync(page, cancellationToken);
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
        public async Task<IActionResult> GetPaymentsByPaymentStatus(PaymentStatus? paymentStatus, [FromQuery] PaymentPageRequest page, CancellationToken cancellationToken)
        {
            if (paymentStatus.HasValue && page.Status.HasValue && paymentStatus.Value != page.Status.Value)
                return BadRequest(new { message = "Status filters conflict." });
            page.Status ??= paymentStatus;
            if (!HasValidDateRange(page)) return BadRequest(new { message = "FromDate must be earlier than or equal to ToDate." });
            var result = await pagedReads.GetPaymentsAsync(page, cancellationToken);
            return Ok(result);
        }

        [HttpGet("Get-Payments-By-BookingId")]
        [Authorize(Roles = "Client,Worker,Admin")]
        public async Task<IActionResult> GetPaymentsByBookingId(Guid bookingId, [FromQuery] PaymentPageRequest page, CancellationToken cancellationToken)
        {
            if (!HasValidDateRange(page)) return BadRequest(new { message = "FromDate must be earlier than or equal to ToDate." });
            if (!await CanAccessBookingAsync(bookingId, cancellationToken))
                return NotFound();

            var result = await pagedReads.GetBookingPaymentsAsync(bookingId, page, cancellationToken);
            return Ok(result);
        }

        [HttpPost("initialize")]
        [Authorize(Roles = "Client")]
        [EnableRateLimiting(RateLimitConfiguration.Sensitive)]
        public async Task<IActionResult> Initialize([FromBody] InitializePaymentCommand command, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return BadRequest(new { message = "Idempotency-Key header is required." });
            var client = await GetCurrentClientAsync(cancellationToken);
            var booking = await UnitOfWork.Bookings.GetBookingById(command.BookingId, cancellationToken);
            if (client is null || booking is null || booking.ClientId != client.Id)
                return NotFound();

            try
            {
                var result = await mediator.Send(new PayForAcceptedBookingCommand(
                    booking.Id, client.Id, CurrentUserEmail!, "Paystack", CurrentUserId, null, idempotencyKey), cancellationToken);
                return Ok(new { checkoutUrl = result.CheckoutUrl, paymentReference = result.PaymentReference });
            }
            catch (PaymentInitializationConflictException)
            {
                return Problem(title: "Payment initialization conflict", detail: "A payment attempt is already in progress or needs reconciliation.", statusCode: StatusCodes.Status409Conflict);
            }
        }

        [HttpPost("bookings/{bookingId:guid}/pay")]
        [Authorize(Roles = "Client")]
        [EnableRateLimiting(RateLimitConfiguration.Sensitive)]
        public async Task<IActionResult> PayForAcceptedBooking(Guid bookingId, [FromBody] ClientPaymentRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return BadRequest(new { message = "Idempotency-Key header is required." });
            var client = await GetCurrentClientAsync(cancellationToken);
            var booking = await UnitOfWork.Bookings.GetBookingById(bookingId, cancellationToken);
            if (client is null || booking is null || booking.ClientId != client.Id)
                return NotFound();

            var command = new PayForAcceptedBookingCommand(
                bookingId,
                client.Id,
                CurrentUserEmail!,
                "Paystack",
                CurrentUserId,
                null,
                idempotencyKey);

            try
            {
                var result = await mediator.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (PaymentInitializationConflictException)
            {
                return Problem(title: "Payment initialization conflict", detail: "A payment attempt is already in progress or needs reconciliation.", statusCode: StatusCodes.Status409Conflict);
            }
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
        [EnableRateLimiting(RateLimitConfiguration.Sensitive)]
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

        [HttpGet("paystack/webhook/quarantined")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetQuarantinedWebhooks(CancellationToken cancellationToken)
        {
            var events = await webhookInbox.GetQuarantinedAsync(100, cancellationToken);
            return Ok(events.Select(x => new
            {
                x.Id,
                x.EventKey,
                x.EventType,
                x.ProviderEventId,
                x.PaymentReference,
                x.ReceivedAtUtc,
                x.Attempts,
                x.LastAttemptedAtUtc,
                x.QuarantinedAtUtc,
                x.LastErrorCategory,
                x.RequeueCount,
                x.LastRequeuedBy
            }));
        }

        [HttpPost("paystack/webhook/{eventId:guid}/requeue")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RequeueQuarantinedWebhook(Guid eventId, CancellationToken cancellationToken)
        {
            var operatorId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(operatorId))
                return Forbid();
            var requeued = await webhookInbox.RequeueQuarantinedAsync(
                eventId, operatorId, DateTime.UtcNow, cancellationToken);
            return requeued ? Accepted() : NotFound();
        }

        [HttpPost("paystack/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PaystackWebhook(CancellationToken cancellationToken)
        {
            await using var memory = new MemoryStream();
            await Request.Body.CopyToAsync(memory, cancellationToken);
            var payload = memory.ToArray();
            var signature = Request.Headers["x-paystack-signature"].ToString();
            if (string.IsNullOrEmpty(signature))
                return BadRequest("Missing signature header.");

            var correlationId = HttpContext.Items[CorrelationIdMiddleware.HeaderName] as string;
            await mediator.Send(new ProcessPaystackWebhookCommand(payload, signature, correlationId), cancellationToken);
            return Ok();
        }
    }
}
