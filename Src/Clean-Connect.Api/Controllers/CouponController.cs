using Clean_Connect.Application.Command.CouponCommand;
using Clean_Connect.Application.Query.CouponQuery;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clean_Connect.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CouponController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CouponController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCoupon([FromBody] CreateCouponCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetCouponByCode), new { code = command.Code }, result);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCoupon(Guid id, [FromBody] UpdateCouponCommand command, CancellationToken cancellationToken)
        {
            if (id != command.Id)
                return BadRequest("ID in URL and body must match.");

            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCoupon(Guid id, CancellationToken cancellationToken)
        {
            var command = new DeleteCouponCommand(id);
            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllCoupons(CancellationToken cancellationToken)
        {
            var query = new GetAllCouponsQuery();
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{code}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCouponByCode(string code, CancellationToken cancellationToken)
        {
            var query = new GetCouponByCodeQuery(code);
            var result = await _mediator.Send(query, cancellationToken);

            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpGet("validate/{code}")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateCoupon(string code, CancellationToken cancellationToken)
        {
            var query = new ValidateCouponQuery(code);
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }
    }
}
