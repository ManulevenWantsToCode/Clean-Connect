using Clean_Connect.Application.Command.ServiceTypeCommands;
using Clean_Connect.Application.Query.ServiceTypeQuery;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Clean_Connect.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceTypeController : ControllerBase
    {
        private readonly ILogger<ServiceTypeController> logger;
        private readonly IMediator mediator;
        public ServiceTypeController(ILogger<ServiceTypeController> _logger, IMediator _mediator)
        {
            logger = _logger;
            mediator = _mediator;
        }

        [HttpPost("Create-service-type")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> CreateServiceType([FromBody] CreateServiceTypeCommand serviceType, CancellationToken cancellationToken)
        {
            logger.LogInformation("Service type creation started");
            var result = await mediator.Send(serviceType, cancellationToken);
            logger.LogInformation("Service type creation completed");
            return StatusCode(201, result);
        }

        [HttpPut("Update-service-type")]
        [Authorize(Roles = "Admin")]

        public async Task<ActionResult> UpdateServiceType([FromBody] UpdateServiceTypeCommands serviceType, CancellationToken cancellationToken)
        {
            logger.LogInformation("Service type update started");
            var result = await mediator.Send(serviceType, cancellationToken);
            logger.LogInformation("Service type update completed");
            return Ok(result);
        }

        [HttpGet("Get-all-service-types")]
        [AllowAnonymous]

        public async Task<ActionResult> GetAllServiceTypes(CancellationToken cancellationToken)
        {
            logger.LogInformation("Get all service types operation started");
            var query = new GetAllServiceTypeQuery();
            var result = await mediator.Send(query, cancellationToken);
            logger.LogInformation("Get all service types operation completed");
            return Ok(result);
        }


        [HttpGet("Get-service-type-by-id/{id}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetServiceTypeById(Guid id, CancellationToken cancellationToken)
        {
            logger.LogInformation("Get service type by ID operation started");
            var query = new GetServiceByIdQuery(id);
            var result = await mediator.Send(query, cancellationToken);
            logger.LogInformation("Get service type by ID operation completed");
            return Ok(result);
        }


    }
}

   
