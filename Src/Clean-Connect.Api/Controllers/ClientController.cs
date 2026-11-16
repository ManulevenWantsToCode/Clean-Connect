using Clean_Connect.Application.Command.ClientCommands;
using Clean_Connect.Application.Command.WorkerCommands;
using Clean_Connect.Application.Query.ClientQuery;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.DTO.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clean_Connect.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientController : ApiControllerBase
    {
        private readonly ILogger<ClientController> logger;
        private readonly IMediator mediator;
        private readonly IPagedReadRepository pagedReads;
        public ClientController(ILogger<ClientController> _logger, IMediator _mediator, IUnitOfWork unitOfWork, IPagedReadRepository pagedReads)
            : base(unitOfWork)
        {
            logger = _logger;
            mediator = _mediator;
            this.pagedReads = pagedReads;
        }

        [HttpPost("register-client")]
        [Authorize(Roles = "Client")]

        public async Task<ActionResult> RegisterClient([FromBody] CreateClientCommand client, CancellationToken cancellationToken)
        {
            logger.LogInformation("Client registration started");
            var result = await mediator.Send(client, cancellationToken);
            logger.LogInformation("Client registration completed");
            return Ok(result);
        }

        [HttpGet("get-client-by-id")]
        [Authorize(Roles = "Client,Admin")]

        public async Task<ActionResult> GetClientById(Guid id, CancellationToken cancellationToken)
        {
            if (!IsAdministrator)
            {
                var clientProfile = await GetCurrentClientAsync(cancellationToken);
                if (clientProfile is null || clientProfile.Id != id)
                    return Forbid();
            }

            logger.LogInformation("Fetching client by ID");
            var client = new GetClientByIdQuery(id);
            var result = await mediator.Send(client, cancellationToken);
            logger.LogInformation("Client fetched successfully");
            return Ok(result);
        }
        [HttpGet("get-client-bookings-by-id")]
        [Authorize(Roles = "Client,Admin")]

        public async Task<ActionResult> GetClientBookingsById(Guid id, [FromQuery] BookingPageRequest page, CancellationToken cancellationToken)
        {
            if (!HasValidDateRange(page)) return BadRequest(new { message = "FromDate must be earlier than or equal to ToDate." });
            if (!IsAdministrator)
            {
                var clientProfile = await GetCurrentClientAsync(cancellationToken);
                if (clientProfile is null || clientProfile.Id != id)
                    return Forbid();
            }

            logger.LogInformation("Fetching client bookings by ID");
            var result = await pagedReads.GetClientBookingsAsync(id, page, cancellationToken);
            logger.LogInformation("Client bookings fetched successfully");
            return Ok(result);
        }

        [HttpGet("all-clients")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetAllClients([FromQuery] ClientPageRequest page, CancellationToken cancellationToken)
        {
            logger.LogInformation("Fetching all clients");

            var result = await pagedReads.GetClientsAsync(page, cancellationToken);
            logger.LogInformation("All clients fetched successfully");
            return Ok(result);
        }

        [HttpPut("update-client")]
        [Authorize(Roles = "Client")]

        public async Task<ActionResult> UpdateClient([FromBody] UpdateClientCommand client, CancellationToken cancellationToken)
        {
            var clientProfile = await GetCurrentClientAsync(cancellationToken);
            if (clientProfile is null)
                return Forbid();

            client = client with { ClientId = clientProfile.Id, ModifiedBy = CurrentUserId };
            logger.LogInformation("Client update started");
            var result = await mediator.Send(client, cancellationToken);
            logger.LogInformation("Client update completed");
            return Ok(result);
        }

        [HttpPost("{bookingId}/markasCompleted")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> MarkBookingAsCompleted(Guid bookingId, [FromBody] MarkAsCompletedCommand command, CancellationToken cancellationToken)
        {
            var clientProfile = await GetCurrentClientAsync(cancellationToken);
            if (clientProfile is null || !await CanAccessBookingAsync(bookingId, cancellationToken))
                return Forbid();

            command = command with { BookingId = bookingId, ClientId = clientProfile.Id };

            logger.LogInformation("Worker {WorkerId} Completed booking {BookingId}",command.ClientId, bookingId);

            var result = await mediator.Send(command, cancellationToken);

            return Ok(result);
        }

        [HttpPost("RateWorker")]
        [Authorize(Roles = "Client")]

        public async Task<IActionResult> RateWorker([FromBody] CreateRatingCommand command, CancellationToken cancellationToken)
        {
            var clientProfile = await GetCurrentClientAsync(cancellationToken);
            if (clientProfile is null || !await CanAccessBookingAsync(command.BookingId, cancellationToken))
                return Forbid();

            command = command with { ClientId = clientProfile.Id, CreatedBy = CurrentUserId };
            logger.LogInformation("Worker rating started");
            var result = await mediator.Send(command, cancellationToken);
            logger.LogInformation("Worker rating completed");
            return Ok(result);
        }
    }
}
