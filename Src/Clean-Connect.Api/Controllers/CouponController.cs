using Clean_Connect.Application.Command.CouponCommand;
using Clean_Connect.Application.Query.CouponQuery;
using MediatR;
using Clean_Connect.Application.Interface.Repositories;
using Clean_Connect.Application.DTO.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.RateLimiting;
using Clean_Connect.Infrastructure.Operational;

namespace Clean_Connect.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CouponController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IPagedReadRepository _pagedReads;

        public CouponController(IMediator mediator, IPagedReadRepository pagedReads)
        {
            _mediator = mediator;
            _pagedReads = pagedReads;
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
        public async Task<IActionResult> GetAllCoupons([FromQuery] CouponPageRequest page, CancellationToken cancellationToken)
        {
            var result = await _pagedReads.GetCouponsAsync(page, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{code}")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitConfiguration.PublicRead)]
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
        [EnableRateLimiting(RateLimitConfiguration.PublicRead)]
        public async Task<IActionResult> ValidateCoupon(string code, CancellationToken cancellationToken)
        {
            var query = new ValidateCouponQuery(code);
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }
    }
}
