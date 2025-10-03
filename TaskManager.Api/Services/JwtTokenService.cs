using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Models;

namespace TaskManager.Api.Services
{
    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly string _key;
        private readonly string _issuer;

        public JwtTokenService(IConfiguration cfg)
        {
            _key = cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured");
            _issuer = cfg["Jwt:Issuer"] ?? "TaskManagerApi";
            // Требование HS256: ключ минимум 256 бит (32 байта)
            if (Encoding.UTF8.GetByteCount(_key) < 32)
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

            // роли → клеймы роли
            if (roles != null && roles.Count > 0)
            {
                foreach (var role in roles)
                    claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _issuer,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}