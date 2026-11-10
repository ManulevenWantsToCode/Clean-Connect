using Clean_Connect.Application.Command.ApplicationUserCommand;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Clean_Connect.Api.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Clean_Connect.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {

        private readonly IMediator _mediator;
        private readonly IJwtRevocationStore _jwtRevocationStore;

        public AccountController(IMediator mediator, IJwtRevocationStore jwtRevocationStore)
        {
            _mediator = mediator;
            _jwtRevocationStore = jwtRevocationStore;
        }
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task <ActionResult> Register([FromBody] RegisterUserCommand registerModel)
        {
          
            var response = await _mediator.Send(registerModel);
            return Ok(response);
        }

        [HttpGet("confirm-email")]
        [AllowAnonymous]
        public async Task <ActionResult> ConfirmEmail([FromQuery] ConfirmEmailCommand confirmEmailModel)
        {
            var response = await _mediator.Send(confirmEmailModel);
            return Ok(response);
        }

        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task <ActionResult> Login([FromBody] LoginCommand loginModel)
        {
            

            var response = await _mediator.Send(loginModel);
            return Ok(response);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task <ActionResult> Logout()
        {
            var userValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var tokenId = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
            var expiryValue = User.FindFirstValue(JwtRegisteredClaimNames.Exp)
                ?? User.FindFirstValue(ClaimTypes.Expiration);
            if (!Guid.TryParse(userValue, out var userId) || string.IsNullOrWhiteSpace(tokenId)
                || !long.TryParse(expiryValue, out var expirySeconds))
                return Unauthorized();

            await _jwtRevocationStore.RevokeAsync(userId, tokenId,
                DateTimeOffset.FromUnixTimeSeconds(expirySeconds), HttpContext.RequestAborted);
            var command = new LogOutCommand();
            var response = await _mediator.Send(command);
            return Ok(response);
        }
    }
}
