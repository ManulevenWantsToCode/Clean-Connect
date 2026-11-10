using Clean_Connect.Application.Command.BookingCommand;
using Clean_Connect.Application.Interface.Repositories;
using MediatR;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class BookingController : ApiControllerBase
    {
        private readonly ILogger<BookingController> logger;
        private readonly IMediator mediator;

        public BookingController(ILogger<BookingController> logger, IMediator mediator, IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
            this.logger = logger;
            this.mediator = mediator;
        }

        [Authorize(Roles = "Client")]
        [HttpPost("create-Booking")]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingCommand booking, CancellationToken cancellationToken)
        {
            var clientProfile = await GetCurrentClientAsync(cancellationToken);
            if (clientProfile is null)
                return Forbid();

            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return BadRequest(new { message = "Idempotency-Key header is required." });

            booking = booking with { ClientId = clientProfile.Id, CreatedBy = CurrentUserId, IdempotencyKey = idempotencyKey };
            logger.LogInformation("Booking creation started");
            BookingCreateResult result;
            try
            {
                result = await mediator.Send(booking, cancellationToken);
            }
            catch (BookingConflictException)
            {
                return Conflict(new { message = "The worker is already booked for part of this time." });
            }
            catch (IdempotencyConflictException)
            {
                return Conflict(new { message = "This Idempotency-Key was already used for a different request." });
            }
            catch (ValidationException)
            {
                return BadRequest(new { message = "The booking request is invalid or does not meet current booking requirements." });
            }
            logger.LogInformation("Booking creation completed");
            return result.Created ? StatusCode(201, result.BookingId) : Ok(result.BookingId);
        }

        [Authorize(Roles = "Worker")]
        [HttpPost("{bookingId}/mark-as-completed-by-Worker")]
        public async Task<IActionResult> MarkJobAsCompleted(Guid bookingId, [FromBody] JobCompletedByWorkerCommand command, CancellationToken cancellationToken)
        {
            var workerProfile = await GetCurrentWorkerAsync(cancellationToken);
            var booking = await UnitOfWork.Bookings.GetBookingById(bookingId, cancellationToken);
            if (workerProfile is null || booking is null || booking.WorkerId != workerProfile.Id)
                return Forbid();

            command = command with { BookingId = bookingId, WorkerId = workerProfile.Id };
            logger.LogInformation("Awaiting client confirmation for booking {BookingId}", bookingId);
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [Authorize(Roles = "Worker")]
        [HttpPost("{bookingId}/Job-In-Progress")]
        public async Task<IActionResult> JobInProgress(Guid bookingId, [FromBody] RequestJobStartCommand command, CancellationToken cancellationToken)
        {
            var workerProfile = await GetCurrentWorkerAsync(cancellationToken);
            var booking = await UnitOfWork.Bookings.GetBookingById(bookingId, cancellationToken);
            if (workerProfile is null || booking is null || booking.WorkerId != workerProfile.Id)
                return Forbid();

            command = command with { BookingId = bookingId, WorkerId = workerProfile.Id };
            logger.LogInformation("Worker requested job start for booking {BookingId}", bookingId);
            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
    }
}
