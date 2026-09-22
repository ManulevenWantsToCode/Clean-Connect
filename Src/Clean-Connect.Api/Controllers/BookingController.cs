using Clean_Connect.Application.Command.BookingCommand;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class BookingController : ControllerBase
    {
        private readonly ILogger<BookingController> logger;
        private readonly IMediator mediator;
        public BookingController(ILogger<BookingController> _logger, IMediator _mediator)
        {
            logger = _logger;
            mediator = _mediator;
        }

        [Authorize(Roles ="Worker, Client")]
        [HttpPost("create-Booking")]

        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingCommand booking, CancellationToken cancellationToken)
        {
            logger.LogInformation("Booking creation started");
            var result = await mediator.Send(booking, cancellationToken);
            logger.LogInformation("Booking creation completed");
            return StatusCode(201, result);
        }

        [HttpPost("{bookingId}/mark-as-completed-by-Worker")]
        public async Task<IActionResult> MarkJobAsCompleted(Guid bookingId, [FromBody] JobCompletedByWorkerCommand command, CancellationToken cancellationToken)
        {
            command = command with { BookingId = bookingId };

            logger.LogInformation("Awaiting client to verify booking {BookingId}", command.WorkerId, bookingId);

            var result = await mediator.Send(command, cancellationToken);

            return Ok(result);
        }
        [HttpPost("{bookingId}/Job-In-Progress")]
        public async Task<IActionResult> JobInProgress(Guid bookingId, [FromBody] RequestJobStartCommand command, CancellationToken cancellationToken)
        {
            command = command with { BookingId = bookingId };

            logger.LogInformation("Awaiting client to verify booking {BookingId}", command.WorkerId, bookingId);

            var result = await mediator.Send(command, cancellationToken);

            return Ok(result);
        }
    }
}