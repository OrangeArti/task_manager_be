using System.Collections.Generic;
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Dtos;
using TaskManager.Api.Models;
using TaskManager.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace TaskManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = Policies.User)] // Authenticated users can view directory; role gates inside actions
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        /// <summary>Paginated user list with search by email/displayName.</summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _userManager.Users.AsNoTracking();

            var isAdmin = User.IsInRole("Admin");
            var isSubscriptionOwner = User.IsInRole("SubscriptionOwner");

            if (!isAdmin && isSubscriptionOwner)
            {
                // Subscription owners see only users in their org(s)
                var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    var currentUserDbId = await _db.Users
                        .Where(u => u.KeycloakSubject == currentUserId)
                        .Select(u => u.Id)
                        .FirstOrDefaultAsync() ?? currentUserId;

                    var orgUserIds = await _db.OrgMembers
                        .Where(m => _db.OrgMembers
                            .Where(om => om.UserId == currentUserDbId)
                            .Select(om => om.OrganizationId)
                            .Contains(m.OrganizationId))
                        .Select(m => m.UserId)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(u => orgUserIds.Contains(u.Id));
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(u =>
                    (u.Email != null && u.Email.Contains(s)) ||
                    (u.UserName != null && u.UserName.Contains(s)) ||
                    (u.DisplayName != null && u.DisplayName.Contains(s)));
            }

            var total = await query.CountAsync();

            var users = await query
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (isAdmin || isSubscriptionOwner)
            {
                var items = new List<UserDto>(users.Count);

                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);

                    items.Add(new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email ?? string.Empty,
                        DisplayName = user.DisplayName ?? string.Empty,
                        EmailConfirmed = user.EmailConfirmed,
                        SubscriptionId = user.SubscriptionId,
                        Roles = roles.ToList()
                    });
                }

                var result = new PagedResult<UserDto>
                {
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    Items = items
                };

                return Ok(result);
            }
            else
            {
                var items = new List<PublicUserDto>(users.Count);

                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    items.Add(new PublicUserDto
                    {
                        Id = user.Id,
                        DisplayName = user.DisplayName ?? user.UserName ?? string.Empty,
                        Roles = roles.ToList()
                    });
                }

                var result = new PagedResult<PublicUserDto>
                {
                    Total = total,
                    Page = page,
                    PageSize = pageSize,
                    Items = items
                };

                return Ok(result);
            }
        }

        /// <summary>Delete a user by Id. Private tasks are removed; public tasks remain without an owner.</summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            var isAdmin = User.IsInRole("Admin");
            var isSubscriptionOwner = User.IsInRole("SubscriptionOwner");

            if (!isAdmin && !isSubscriptionOwner)
                return Forbid();

            var currentSubscriptionId = User.FindFirst("subscription_id")?.Value;

            // safeguard: cannot delete yourself
            var me = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            if (me == id)
                return Problem(detail: "You cannot delete yourself.", statusCode: StatusCodes.Status400BadRequest, title: "Invalid operation");

            // Perform everything within ExecutionStrategy + transaction
            var strategy = _db.Database.CreateExecutionStrategy();

            IActionResult? resultToReturn = null; // capture the result from the lambda

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                var user = await _userManager.FindByIdAsync(id);
                if (user is null)
                {
                    // no changes made — just exit
                    resultToReturn = NotFound(new { message = $"User '{id}' not found" });
                    await tx.RollbackAsync();
                    return;
                }

                var targetRoles = await _userManager.GetRolesAsync(user);
                var targetIsAdmin = targetRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
                var targetIsOwner = targetRoles.Contains("SubscriptionOwner", StringComparer.OrdinalIgnoreCase);
                var targetSubscriptionId = user.SubscriptionId;

                if (targetIsAdmin && !isAdmin)
                {
                    resultToReturn = Forbid();
                    await tx.RollbackAsync();
                    return;
                }

                if (targetIsOwner && !isAdmin && !isSubscriptionOwner)
                {
                    resultToReturn = Forbid();
                    await tx.RollbackAsync();
                    return;
                }

                if (!isAdmin && isSubscriptionOwner)
                {
                    if (string.IsNullOrWhiteSpace(currentSubscriptionId) || currentSubscriptionId != targetSubscriptionId)
                    {
                        resultToReturn = Forbid();
                        await tx.RollbackAsync();
                        return;
                    }
                }

                if (targetIsAdmin)
                {
                    var admins = await _userManager.GetUsersInRoleAsync("Admin");
                    if (admins.Count <= 1)
                    {
                        resultToReturn = Problem(detail: "Cannot delete the last Admin.", statusCode: StatusCodes.Status400BadRequest, title: "Invalid operation");
                        await tx.RollbackAsync();
                        return;
                    }
                }

                if (targetIsOwner)
                {
                    var owners = await _userManager.GetUsersInRoleAsync("SubscriptionOwner");
                    var ownersInTargetSubscription = string.IsNullOrWhiteSpace(targetSubscriptionId)
                        ? owners
                        : owners.Where(o => o.SubscriptionId == targetSubscriptionId).ToList();

                    if (ownersInTargetSubscription.Count <= 1)
                    {
                        resultToReturn = Problem(detail: "Cannot delete the last SubscriptionOwner.", statusCode: StatusCodes.Status400BadRequest, title: "Invalid operation");
                        await tx.RollbackAsync();
                        return;
                    }
                }

                // 1) Fetch all tasks created by the user
                var createdTasks = await _db.Tasks
                    .Where(t => t.CreatedById == id)
                    .ToListAsync();

                var personal = createdTasks
                    .Where(t => t.VisibilityScope == TaskVisibilityScopes.Private)
                    .ToList();

                var shared = createdTasks
                    .Where(t => t.VisibilityScope != TaskVisibilityScopes.Private)
                    .ToList();

                if (personal.Count > 0)
                {
                    _db.Tasks.RemoveRange(personal);
                }

                if (shared.Count > 0)
                {
                    foreach (var task in shared)
                    {
                        task.CreatedById = me ?? task.CreatedById;
                        if (task.AssignedToId == id)
                        {
                            task.AssignedToId = null;
                        }
                    }

                    _db.Tasks.UpdateRange(shared);
                }

                // 4) Clear assignee for tasks created by others but assigned to the user
                var assignedToUser = await _db.Tasks
                    .Where(t => t.CreatedById != id && t.AssignedToId == id)
                    .ToListAsync();

                if (assignedToUser.Count > 0)
                {
                    foreach (var task in assignedToUser)
                        task.AssignedToId = null;

                    _db.Tasks.UpdateRange(assignedToUser);
                }

                await _db.SaveChangesAsync();

                // 5) Delete the user through Identity
                var del = await _userManager.DeleteAsync(user);
                if (!del.Succeeded)
                {
                    var errors = string.Join("; ", del.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    resultToReturn = Problem(detail: errors, statusCode: StatusCodes.Status500InternalServerError);
                    await tx.RollbackAsync();
                    return;
                }

                await tx.CommitAsync();
                resultToReturn = NoContent();
            });

            // we land here after ExecuteAsync
            return resultToReturn ?? NoContent();
        }
    }
}
