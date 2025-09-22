using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Models;

namespace TaskManager.Api.Services
{
    public interface IJwtTokenService
    {
        string CreateToken(ApplicationUser user);
    }

    public class JwtTokenService : IJwtTokenService
    {
        private readonly string _issuer;
        private readonly string _key;

        public JwtTokenService(IConfiguration config)
        {
            _issuer = config["Jwt:Issuer"] ?? "TaskManagerApi";
            _key = config["Jwt:Key"] ?? "SuperSecretKey123!";
        }

        public string CreateToken(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new("display_name", user.DisplayName ?? string.Empty)
                // позже добавим tenant_id, роли, пермишны
            };

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _issuer,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(12), // срок жизни MVP
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}