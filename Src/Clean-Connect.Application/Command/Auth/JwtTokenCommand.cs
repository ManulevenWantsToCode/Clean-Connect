using Clean_Connect.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Clean_Connect.Application.Command.Auth
{
    public record JwtTokenCommand(ApplicationUser User) : IRequest<string>;

    public class JwtTokenCommandHandler : IRequestHandler<JwtTokenCommand, string>
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;

        public JwtTokenCommandHandler(IConfiguration configuration, UserManager<ApplicationUser> userManager)
        {
            _configuration = configuration;
            _userManager = userManager;
        }

        public async Task<string> Handle(JwtTokenCommand request, CancellationToken cancellationToken)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var configuredKey = jwtSettings["Key"];
            if (string.IsNullOrWhiteSpace(configuredKey))
                throw new InvalidOperationException("JWT signing configuration is missing.");

            if (!double.TryParse(jwtSettings["DurationInMinutes"], System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var durationInMinutes) || durationInMinutes <= 0)
                throw new InvalidOperationException("JWT lifetime configuration is invalid.");

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuredKey)
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, request.User.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, request.User.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, request.User.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, request.User.UserName ?? request.User.Email ?? string.Empty)
            };

            var roles = await _userManager.GetRolesAsync(request.User);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
            }

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(durationInMinutes),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return tokenString;
        }
    }
}
