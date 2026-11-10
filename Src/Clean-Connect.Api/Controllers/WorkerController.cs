using Clean_Connect.Application.Command.WorkerCommands;
using Clean_Connect.Application.DTO;
using Clean_Connect.Application.Query.WorkersQuery;
using Clean_Connect.Domain.Entities;
using Clean_Connect.Application.Interface.Repositories;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Clean_Connect.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkerController : ApiControllerBase
    {

        private readonly IMediator _mediator;
        private readonly ILogger<WorkerController> _logger;
        public WorkerController(IMediator mediator, ILogger<WorkerController> logger, IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
            _logger = logger;
            _mediator = mediator;
        }


        [HttpPost("register-worker")]
        [Authorize(Roles = "Worker")]

        public async Task<ActionResult> RegisterWorker([FromBody] CreateWorkerCommand worker, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Registeration operation started");

            var result = await _mediator.Send(worker, cancellationToken);
            _logger.LogInformation("Registeration operation completed");
            return Ok(result);
        }

        [HttpPost("{bookingId}/accept")]
        [Authorize(Roles = "Worker")]
        public async Task<IActionResult> AcceptBooking(Guid bookingId, [FromBody] AcceptBookingCommand command, CancellationToken cancellationToken)
        {
            var workerProfile = await GetCurrentWorkerAsync(cancellationToken);
            if (workerProfile is null)
                return Forbid();

            var booking = await UnitOfWork.Bookings.GetBookingById(bookingId, cancellationToken);
            if (booking is null || booking.WorkerId != workerProfile.Id)
                return Forbid();

            command = command with { BookingId = bookingId, WorkerId = workerProfile.Id };

            _logger.LogInformation("Worker {WorkerId} accepting booking {BookingId}",
                command.WorkerId, bookingId);

            bool result;
            try
            {
                result = await _mediator.Send(command, cancellationToken);
            }
            catch (BookingConflictException)
            {
                return Conflict(new { message = "The worker is already booked for part of this time." });
            }
            catch (InvalidOperationException)
            {
                return Conflict(new { message = "The booking is no longer in a state that allows this action." });
            }

            return Ok(result);
        }
        [HttpPost("{bookingId}/Reject")]
        [Authorize(Roles = "Worker")]
        public async Task<IActionResult> RejectBooking(Guid bookingId, [FromBody] RejectBookingCommand command, CancellationToken cancellationToken)
        {
            var workerProfile = await GetCurrentWorkerAsync(cancellationToken);
            if (workerProfile is null)
                return Forbid();

            var booking = await UnitOfWork.Bookings.GetBookingById(bookingId, cancellationToken);
            if (booking is null || booking.WorkerId != workerProfile.Id)
                return Forbid();

            command = command with { BookingId = bookingId, WorkerId = workerProfile.Id };

            _logger.LogInformation("Worker {WorkerId} Rejecting booking {BookingId}",
                command.WorkerId, bookingId);

            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }

        [HttpPut("update-worker")]
        [Authorize(Roles = "Worker")]

        public async Task<ActionResult> UpdateWorker([FromBody] UpdateWorkerCommand worker, CancellationToken cancellationToken)
        {
            var workerProfile = await GetCurrentWorkerAsync(cancellationToken);
            if (workerProfile is null)
                return Forbid();

            worker = worker with
            {
                WorkerId = workerProfile.Id,
                Email = workerProfile.Email.ToString(),
                ModifiedBy = CurrentUserId
            };
            _logger.LogInformation("Update operation started");
            var result = await _mediator.Send(worker, cancellationToken);
            _logger.LogInformation("Update operation completed");
            return Ok(result);
        }

        [HttpGet("all-worker")]
        [Authorize(Roles = "Admin")]

        public async Task<ActionResult> GetAllWorkers(CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"GetAllWorkers: {query}");

            var allWorker = await _mediator.Send(new GetAllWorkersQuery(), cancellationToken);
            _logger.LogInformation("All worker fetched succesfully");
            return Ok(allWorker);
        }

        [HttpGet("get-worker-by-id")]
        [Authorize(Roles = "Client,Admin")]
        public async Task<ActionResult> GetWorkersById(Guid id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting worker by id");

            if (User.IsInRole("Admin"))
            {
                var adminWorker = await _mediator.Send(new GetWorkerByIdQuery(id), cancellationToken);
                return Ok(adminWorker);
            }

            var worker = await _mediator.Send(new GetWorkerMarketplaceProfileQuery(id), cancellationToken);
            if (worker is null) return NotFound();

            _logger.LogInformation("Worker fetched succesfully");
            return Ok(worker);
        }

        [HttpGet("me")]
        [Authorize(Roles = "Worker")]
        public async Task<ActionResult<WorkerPrivateProfileDto>> GetMyProfile(CancellationToken cancellationToken)
        {
            var worker = await GetCurrentWorkerAsync(cancellationToken);
            if (worker is null) return Forbid();
            return Ok(new WorkerPrivateProfileDto
            {
                WorkerId = worker.Id,
                Name = worker.FullName,
                Email = worker.Email,
                Address = worker.Address,
                Contact = worker.Contact,
                Gender = worker.Gender.ToString(),
                ServiceType = worker.ServiceType.Name,
                State = worker.State,
                DateOfBirth = worker.DateOfBirth,
                HourlyRate = worker.HourlyRate,
                IsAvailable = worker.IsAvailable
            });
        }
        [HttpGet("get-worker-booking-by-id")]
        [Authorize(Roles = "Worker")]
        public async Task<ActionResult> GetWorkerBookingsById(Guid id, CancellationToken cancellationToken)
        {
            var workerProfile = await GetCurrentWorkerAsync(cancellationToken);
            if (workerProfile is null || workerProfile.Id != id)
                return Forbid();

            _logger.LogInformation("Getting worker booking by id");

            var worker = await _mediator.Send(new GetAllWorkerBookingsQuery(id), cancellationToken);

            _logger.LogInformation("Worker booking fetched succesfully");
            return Ok(worker);
        }

        [HttpPost("nearby")]
        [Authorize(Roles = "Client")]
        public async Task<ActionResult<List<NearbyWorkerDto>>> GetNearbyWorkers([FromBody] GetNearByWorkersQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

    }
}
