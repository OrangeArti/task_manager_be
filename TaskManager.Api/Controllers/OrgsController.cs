using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManager.Api.Authorization;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos.Orgs;
using TaskManager.Api.Models;
using Microsoft.Extensions.Logging;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/orgs")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public class OrgsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OrgsController> _logger;

    public OrgsController(ApplicationDbContext db, ILogger<OrgsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private async Task<string?> GetCurrentUserDbIdAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (sub is null) return null;
        return await _db.Users
            .Where(u => u.KeycloakSubject == sub)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>Creates a new Organization. Caller becomes the SubscriptionOwner.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrgDto>> CreateAsync([FromBody] CreateOrgDto dto)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            ModelState.AddModelError(nameof(dto.Name), "Organization name cannot be empty.");
            return ValidationProblem(ModelState);
        }

        var org = new Organization { Name = dto.Name.Trim(), OwnerId = userId };
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(); // get org.Id

        _db.Subscriptions.Add(new Subscription { OrganizationId = org.Id, PlanType = "Free" });
        _db.OrgMembers.Add(new OrgMember
        {
            OrganizationId = org.Id,
            UserId = userId,
            Role = OrgRoles.SubscriptionOwner,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = new OrgDto(org.Id, org.Name, org.OwnerId, org.CreatedAt);
        return CreatedAtRoute("GetOrgByIdAsync", new { id = org.Id }, result);
    }

    /// <summary>Gets an Organization by ID.</summary>
    [HttpGet("{id:int}", Name = "GetOrgByIdAsync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgDto>> GetByIdAsync(int id)
    {
        var org = await _db.Organizations.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrgDto(o.Id, o.Name, o.OwnerId, o.CreatedAt))
            .FirstOrDefaultAsync();

        if (org is null) return NotFound();
        return Ok(org);
    }

    /// <summary>Generates an invite link for the org. Caller must be SubscriptionOwner.</summary>
    [HttpPost("{orgId:int}/invites")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InviteResponseDto>> GenerateInviteAsync(
        int orgId,
        [FromBody] GenerateInviteDto dto)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        var orgExists = await _db.Organizations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return NotFound();

        var isOwner = await _db.OrgMembers.AnyAsync(m =>
            m.OrganizationId == orgId &&
            m.UserId == userId &&
            m.Role == OrgRoles.SubscriptionOwner);

        if (!isOwner) return Forbid();

        var token = Guid.NewGuid().ToString("N"); // 32-char hex
        _db.OrgInvitations.Add(new OrgInvitation
        {
            OrganizationId = orgId,
            Token = token,
            InviteeEmail = dto.Email?.Trim() ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var inviteUrl = $"/join?token={token}";
        _logger.LogInformation("Invite generated for org {OrgId}: {InviteUrl}", orgId, inviteUrl);

        return StatusCode(StatusCodes.Status201Created, new InviteResponseDto(inviteUrl));
    }
}
