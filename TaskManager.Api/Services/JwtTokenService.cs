using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Models;
using TaskManager.Shared.Security;

namespace TaskManager.Api.Services
{
    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly JwtOptions _options;

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value ?? throw new InvalidOperationException("Jwt options are not configured.");
            // HS256 requires the key to be at least 256 bits (32 bytes)
            if (Encoding.UTF8.GetByteCount(_options.Key) < 32)
                throw new InvalidOperationException("Jwt:Key length must be at least 32 bytes for HS256.");
        }

        public string CreateToken(ApplicationUser user, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new("display_name", user.DisplayName ?? string.Empty)
            };

            // roles -> role claims
            if (roles != null && roles.Count > 0)
            {
                foreach (var role in roles)
                    claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
