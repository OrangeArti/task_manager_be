using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using TaskManager.Api.Dtos;
using TaskManager.Api.Models;
using TaskManager.Api.Services;

namespace TaskManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenService _jwt;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenService jwt)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwt = jwt;
        }

        /// <summary>Register new user and return JWT</summary>
        [HttpPost("register")]
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

            var token = _jwt.CreateToken(user);
            return Ok(new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName
            });
        }

        /// <summary>Login and return JWT</summary>
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Unauthorized("Invalid credentials.");

            var pwd = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
            if (!pwd.Succeeded)
                return Unauthorized("Invalid credentials.");

            var token = _jwt.CreateToken(user);
            return Ok(new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName
            });
        }
    }
}