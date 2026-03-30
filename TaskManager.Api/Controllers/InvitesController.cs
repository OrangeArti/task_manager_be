using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManager.Api.Authorization;
using TaskManager.Api.Data;
using TaskManager.Api.Models;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/invites")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public class InvitesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public InvitesController(ApplicationDbContext db) => _db = db;

    private async Task<string?> GetCurrentUserDbIdAsync()
    {
        var sub = User.FindFirstValue("sub");
        if (sub is null) return null;
        return await _db.Users
            .Where(u => u.KeycloakSubject == sub)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>Accepts an invite token. Creates OrgMember(Member) for the caller.</summary>
    [HttpGet("accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> AcceptAsync([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ModelState.AddModelError(nameof(token), "Token is required.");
            return ValidationProblem(ModelState);
        }

        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        var invitation = await _db.OrgInvitations
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation is null || invitation.IsUsed || invitation.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { error = "Invalid, expired, or already used invite token." });

        var alreadyMember = await _db.OrgMembers.AnyAsync(m =>
            m.OrganizationId == invitation.OrganizationId &&
            m.UserId == userId);

        if (!alreadyMember)
        {
            _db.OrgMembers.Add(new OrgMember
            {
                OrganizationId = invitation.OrganizationId,
                UserId = userId,
                Role = OrgRoles.Member,
                JoinedAt = DateTime.UtcNow
            });
        }

        invitation.IsUsed = true;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            orgId = invitation.OrganizationId,
            orgName = invitation.Organization?.Name
        });
    }
}
