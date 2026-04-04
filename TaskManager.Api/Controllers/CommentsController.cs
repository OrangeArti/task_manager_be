using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManager.Api.Authorization;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos.Comments;
using TaskManager.Api.Models;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId:int}/comments")]
[Authorize(Policy = Policies.User)]
[Produces("application/json")]
public class CommentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CommentsController(ApplicationDbContext db) => _db = db;

    // ─── Helpers (copied from TasksController — same pattern) ───────────────

    private async Task<string?> GetCurrentUserDbIdAsync()
    {
        var sub = User.FindFirstValue("sub");
        if (sub is null) return null;
        return await _db.Users
            .Where(u => u.KeycloakSubject == sub)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<(IReadOnlySet<int> groupIds, bool isSubscriptionOwner)> GetUserGroupContextAsync(string userId)
    {
        var groupIds = await _db.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.UserId == userId)
            .Select(gm => gm.GroupId)
            .ToListAsync();

        var isSubOwner = await _db.OrgMembers
            .AnyAsync(m => m.UserId == userId && m.Role == OrgRoles.SubscriptionOwner);

        return (new HashSet<int>(groupIds), isSubOwner);
    }

    // ─── Task visibility gate (identical filter to TasksController.GetAll) ──

    private async Task<TaskItem?> GetVisibleTaskAsync(int taskId, string userId, bool isAdmin)
    {
        var taskQuery = _db.Tasks.AsNoTracking().Where(t => t.Id == taskId);

        if (!isAdmin)
        {
            var (userGroupIds, isSubscriptionOwner) = await GetUserGroupContextAsync(userId);
            var groupIdsList = userGroupIds.ToList();
            taskQuery = taskQuery.Where(t =>
                t.CreatedById == userId ||
                t.AssignedToId == userId ||
                (t.VisibilityScope == TaskVisibilityScopes.TeamPublic &&
                 (t.AssignedToId == null || t.IsAssigneeVisibleToOthers) &&
                 ((t.GroupId.HasValue && groupIdsList.Contains(t.GroupId.Value)) || isSubscriptionOwner)) ||
                (t.VisibilityScope == TaskVisibilityScopes.GlobalPublic &&
                 (t.AssignedToId == null || t.IsAssigneeVisibleToOthers)));
        }

        return await taskQuery.FirstOrDefaultAsync();
    }

    // ─── GET /api/tasks/{taskId}/comments ────────────────────────────────────

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> GetComments([FromRoute] int taskId)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        var isAdmin = User.IsInRole("Admin");
        var task = await GetVisibleTaskAsync(taskId, userId, isAdmin);
        if (task is null) return NotFound();

        var comments = await _db.Comments
            .AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(
                c.Id,
                c.TaskId,
                c.AuthorId,
                c.Author!.UserName ?? c.AuthorId,
                c.Content,
                c.CreatedAt))
            .ToListAsync();

        return Ok(comments);
    }

    // ─── POST /api/tasks/{taskId}/comments ───────────────────────────────────

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentDto>> Create(
        [FromRoute] int taskId,
        [FromBody] CreateCommentRequest request)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        var isAdmin = User.IsInRole("Admin");
        var task = await GetVisibleTaskAsync(taskId, userId, isAdmin);
        if (task is null) return NotFound();

        var comment = new Comment
        {
            TaskId = taskId,
            AuthorId = userId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow   // explicit — In-Memory DB ignores HasDefaultValueSql
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        // Re-query to get the Author navigation property for the DTO
        var authorName = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.UserName ?? userId)
            .FirstOrDefaultAsync() ?? userId;

        var dto = new CommentDto(
            comment.Id,
            comment.TaskId,
            comment.AuthorId,
            authorName,
            comment.Content,
            comment.CreatedAt);

        return CreatedAtAction(nameof(GetComments), new { taskId }, dto);
    }

    // ─── DELETE /api/tasks/{taskId}/comments/{commentId} ─────────────────────

    [HttpDelete("{commentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] int taskId,
        [FromRoute] int commentId)
    {
        var userId = await GetCurrentUserDbIdAsync();
        if (userId is null) return Unauthorized();

        var isAdmin = User.IsInRole("Admin");

        // Task visibility check FIRST — prevents leaking comment existence
        var task = await GetVisibleTaskAsync(taskId, userId, isAdmin);
        if (task is null) return NotFound();

        var comment = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId);

        if (comment is null) return NotFound();

        // Delete authorization: own comment OR Admin
        if (comment.AuthorId != userId && !isAdmin)
            return Forbid();

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
