using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using TaskManager.Api.Dtos;
using TaskManager.Api.Models;
using TaskManager.Api.Services;
using TaskManager.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace TaskManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenService _jwt;
        private readonly ApplicationDbContext _db;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenService jwt,
            ApplicationDbContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwt = jwt;
            _db = db;
        }

        /// <summary>Register new user and return JWT</summary>
        [HttpPost("register")]
        [EnableRateLimiting("AuthTight")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            var exists = await _userManager.FindByEmailAsync(request.Email);
            if (exists != null)
                return Conflict("User with this email already exists.");

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                DisplayName = request.DisplayName
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // If user is created successfully, add the User role
            await _userManager.AddToRoleAsync(user, "User");

            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwt.CreateToken(user, roles);

            // remove expired refresh tokens
            var expired = _db.RefreshTokens.Where(r => r.ExpiresAt < DateTime.UtcNow);
            _db.RefreshTokens.RemoveRange(expired);
            await _db.SaveChangesAsync();

            // revoke existing active refresh tokens
            var oldTokens = _db.RefreshTokens.Where(r => r.UserId == user.Id && !r.IsRevoked);
            foreach (var t in oldTokens)
                t.IsRevoked = true;
            await _db.SaveChangesAsync();

            var refresh = new RefreshToken
            {
                Token = Guid.NewGuid().ToString(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
            _db.RefreshTokens.Add(refresh);
            await _db.SaveChangesAsync();

            return Ok(new AuthResponse
            {
                Token = token,
                RefreshToken = refresh.Token,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName
            });
        }

        /// <summary>Login and return JWT</summary>
        [HttpPost("login")]
        [EnableRateLimiting("AuthTight")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Unauthorized("Invalid credentials.");

            var pwd = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
            if (!pwd.Succeeded)
                return Unauthorized("Invalid credentials.");

            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwt.CreateToken(user, roles);

            // remove expired refresh tokens
            var expired = _db.RefreshTokens.Where(r => r.ExpiresAt < DateTime.UtcNow);
            _db.RefreshTokens.RemoveRange(expired);
            await _db.SaveChangesAsync();

            // revoke existing active refresh tokens
            var oldTokens = _db.RefreshTokens.Where(r => r.UserId == user.Id && !r.IsRevoked);
            foreach (var t in oldTokens)
                t.IsRevoked = true;
            await _db.SaveChangesAsync();

            var refresh = new RefreshToken
            {
                Token = Guid.NewGuid().ToString(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
            _db.RefreshTokens.Add(refresh);
            await _db.SaveChangesAsync();
            return Ok(new AuthResponse
            {
                Token = token,
                RefreshToken = refresh.Token,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName
            });
        }

        /// <summary>Refresh JWT using a valid refresh token (with rotation)</summary>
        [HttpPost("refresh")]
        [EnableRateLimiting("AuthSoft")]
        public async Task<ActionResult<AuthResponse>> Refresh([FromBody] string refreshToken)
        {
            var stored = await _db.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == refreshToken);

            if (stored == null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
                return Unauthorized("Invalid refresh token.");

            // mark the old token as revoked
            stored.IsRevoked = true;
            await _db.SaveChangesAsync();

            // remove expired refresh tokens
            var expired = _db.RefreshTokens.Where(r => r.ExpiresAt < DateTime.UtcNow);
            _db.RefreshTokens.RemoveRange(expired);
            await _db.SaveChangesAsync();

            // revoke existing active refresh tokens
            var oldTokens = _db.RefreshTokens.Where(r => r.UserId == stored.UserId && !r.IsRevoked);
            foreach (var t in oldTokens)
                t.IsRevoked = true;
            await _db.SaveChangesAsync();

            // create a new refresh token
            var newRefresh = new RefreshToken
            {
                Token = Guid.NewGuid().ToString("N"),
                UserId = stored.UserId,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            };
            _db.RefreshTokens.Add(newRefresh);
            await _db.SaveChangesAsync();

            var roles = await _userManager.GetRolesAsync(stored.User!);
            var newAccessToken = _jwt.CreateToken(stored.User!, roles);

            return Ok(new AuthResponse
            {
                Token = newAccessToken,
                RefreshToken = newRefresh.Token,
                UserId = stored.User!.Id,
                Email = stored.User!.Email ?? string.Empty,
                DisplayName = stored.User!.DisplayName
            });
        }

        /// <summary>Logout (revoke refresh token)</summary>
        [HttpPost("logout")]
        [EnableRateLimiting("AuthSoft")]
        public async Task<IActionResult> Logout([FromBody] string refreshToken)
        {
            var stored = await _db.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == refreshToken && !r.IsRevoked);

            if (stored == null)
                return NotFound("Refresh token not found or already revoked.");

            stored.IsRevoked = true;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Logged out successfully" });
        }

    }
}
