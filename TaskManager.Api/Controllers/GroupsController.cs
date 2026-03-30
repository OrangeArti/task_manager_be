using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManager.Api.Authorization;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos.Groups;
using TaskManager.Api.Models;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/orgs/{orgId:int}/groups")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public class GroupsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public GroupsController(ApplicationDbContext db) => _db = db;

    private async Task<string?> GetCurrentUserDbIdAsync()
    {
        var sub = User.FindFirstValue("sub");
        if (sub is null) return null;
        return await _db.Users
            .Where(u => u.KeycloakSubject == sub)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<bool> IsSubscriptionOwnerAsync(int orgId, string userId) =>
        await _db.OrgMembers.AnyAsync(m =>
            m.OrganizationId == orgId &&
            m.UserId == userId &&
            m.Role == OrgRoles.SubscriptionOwner);

    private static GroupDto ToDto(Group g) =>
        new(g.Id, g.Name, g.OrganizationId, g.Description, g.CreatedAt);

    /// <summary>Lists all groups for the org.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<GroupDto>>> GetAllAsync(int orgId)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        var groups = await _db.Groups
            .AsNoTracking()
            .Where(g => g.OrganizationId == orgId)
            .Select(g => new GroupDto(g.Id, g.Name, g.OrganizationId, g.Description, g.CreatedAt))
            .ToListAsync();

        return Ok(groups);
    }

    /// <summary>Gets a single group by ID.</summary>
    [HttpGet("{id:int}", Name = nameof(GetByIdAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GroupDto>> GetByIdAsync(int orgId, int id)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        var group = await _db.Groups
            .AsNoTracking()
            .Where(g => g.Id == id && g.OrganizationId == orgId)
            .Select(g => new GroupDto(g.Id, g.Name, g.OrganizationId, g.Description, g.CreatedAt))
            .FirstOrDefaultAsync();

        if (group is null) return NotFound();
        return Ok(group);
    }

    /// <summary>Creates a new Group in the org. Caller must be SubscriptionOwner.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDto>> CreateAsync(int orgId, [FromBody] CreateGroupDto dto)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            ModelState.AddModelError(nameof(dto.Name), "Group name cannot be empty.");
            return ValidationProblem(ModelState);
        }

        var orgExists = await _db.Organizations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return NotFound();

        if (!await IsSubscriptionOwnerAsync(orgId, userId)) return Forbid();

        var group = new Group
        {
            Name = dto.Name.Trim(),
            OrganizationId = orgId,
            Description = dto.Description?.Trim()
        };
        _db.Groups.Add(group);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByIdAsync), new { orgId, id = group.Id }, ToDto(group));
    }

    /// <summary>Updates a Group's name and/or description. Caller must be SubscriptionOwner.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDto>> UpdateAsync(int orgId, int id, [FromBody] UpdateGroupDto dto)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        if (dto.Name is null && dto.Description is null)
        {
            ModelState.AddModelError(string.Empty, "At least one field must be provided.");
            return ValidationProblem(ModelState);
        }

        if (dto.Name is { } name && string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(dto.Name), "Group name cannot be empty.");
            return ValidationProblem(ModelState);
        }

        if (!await IsSubscriptionOwnerAsync(orgId, userId)) return Forbid();

        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id && g.OrganizationId == orgId);
        if (group is null) return NotFound();

        if (dto.Name is not null) group.Name = dto.Name.Trim();
        if (dto.Description is not null) group.Description = dto.Description.Trim();
        await _db.SaveChangesAsync();

        return Ok(ToDto(group));
    }

    /// <summary>Deletes a Group. Caller must be SubscriptionOwner.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(int orgId, int id)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        if (!await IsSubscriptionOwnerAsync(orgId, userId)) return Forbid();

        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id && g.OrganizationId == orgId);
        if (group is null) return NotFound();

        _db.Groups.Remove(group);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Adds a user to a Group. Caller must be SubscriptionOwner.</summary>
    [HttpPost("{id:int}/members")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMemberAsync(int orgId, int id, [FromBody] AddGroupMemberDto dto)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        if (!await IsSubscriptionOwnerAsync(orgId, userId)) return Forbid();

        var groupExists = await _db.Groups.AnyAsync(g => g.Id == id && g.OrganizationId == orgId);
        if (!groupExists) return NotFound();

        var targetUserExists = await _db.Users.AnyAsync(u => u.Id == dto.UserId);
        if (!targetUserExists)
        {
            ModelState.AddModelError(nameof(dto.UserId), "User not found.");
            return ValidationProblem(ModelState);
        }

        var alreadyMember = await _db.GroupMembers.AnyAsync(gm =>
            gm.GroupId == id && gm.UserId == dto.UserId);

        if (!alreadyMember)
        {
            _db.GroupMembers.Add(new GroupMember
            {
                GroupId = id,
                UserId = dto.UserId,
                JoinedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return StatusCode(StatusCodes.Status201Created, new { groupId = id, userId = dto.UserId });
    }

    /// <summary>Removes a user from a Group. Caller must be SubscriptionOwner.</summary>
    [HttpDelete("{id:int}/members/{memberId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMemberAsync(int orgId, int id, string memberId)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        if (!await IsSubscriptionOwnerAsync(orgId, userId)) return Forbid();

        var membership = await _db.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == id && gm.UserId == memberId);

        if (membership is null) return NotFound();

        _db.GroupMembers.Remove(membership);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
