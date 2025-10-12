using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TaskManager.Api.Dtos;
using TaskManager.Api.Models;

namespace TaskManager.Api.Controllers
{
    /// <summary>
    /// Manage user roles (Admin only). Allows assigning, removing, and listing roles.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminPolicy")]
    public class RolesController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<RolesController> _logger;

        public RolesController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ILogger<RolesController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        // GET: api/roles
        [HttpGet]
        public IActionResult GetAllRoles()
        {
            var roles = _roleManager.Roles.Select(r => new { r.Id, r.Name }).ToList();
            return Ok(roles);
        }

        // GET: api/roles/{userId}
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserRoles(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found.");

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new { user.Email, Roles = roles });
        }

        // POST: api/roles/assign
        [HttpPost("assign")]
        public async Task<IActionResult> AssignRole([FromBody] RoleAssignRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null) return NotFound("User not found.");

            if (!await _roleManager.RoleExistsAsync(request.RoleName))
                return BadRequest("Role does not exist.");

            var result = await _userManager.AddToRoleAsync(user, request.RoleName);
            if (!result.Succeeded) return BadRequest(result.Errors);

            _logger.LogInformation("Role {Role} assigned to {Email}", request.RoleName, user.Email);
            return Ok(new { Message = $"Role {request.RoleName} assigned to {user.Email}" });
        }

        // POST: api/roles/remove
        [HttpPost("remove")]
        public async Task<IActionResult> RemoveRole([FromBody] RoleAssignRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null) return NotFound("User not found.");

            var result = await _userManager.RemoveFromRoleAsync(user, request.RoleName);
            if (!result.Succeeded) return BadRequest(result.Errors);

            _logger.LogInformation("Role {Role} removed from {Email}", request.RoleName, user.Email);
            return Ok(new { Message = $"Role {request.RoleName} removed from {user.Email}" });
        }
    }
}
