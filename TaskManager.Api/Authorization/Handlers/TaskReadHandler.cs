using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Authorization.Requirements;
using TaskManager.Api.Data;
using TaskManager.Api.Models;

namespace TaskManager.Api.Authorization.Handlers
{
    /// <summary>
    /// Resolves task id from route ("id") and applies role-based visibility rules.
    /// </summary>
    public sealed class TaskReadHandler : AuthorizationHandler<TaskReadRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _dbContext;

        public TaskReadHandler(IHttpContextAccessor httpContextAccessor, ApplicationDbContext dbContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _dbContext = dbContext;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TaskReadRequirement requirement)
        {
            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return;
            }

            if (!httpContext.Request.RouteValues.TryGetValue("id", out var idObj) || idObj is null)
            {
                return;
            }

            if (!int.TryParse(idObj.ToString(), out var taskId))
            {
                return;
            }

            var currentUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(currentUserId))
            {
                return;
            }

            var task = await _dbContext.Tasks
                .AsNoTracking()
                .Where(t => t.Id == taskId)
                .Select(t => new
                {
                    t.CreatedById,
                    t.AssignedToId,
                    t.IsAssigneeVisibleToOthers,
                    t.VisibilityScope,
                    t.TeamId
                })
                .FirstOrDefaultAsync();

            if (task is null)
            {
                return;
            }

            var currentTeamId = await _dbContext.Users
                .Where(u => u.Id == currentUserId)
                .Select(u => u.TeamId)
                .FirstOrDefaultAsync();

            var isSubscriptionOwner = context.User.IsInRole("SubscriptionOwner");

            if (task.CreatedById == currentUserId ||
                task.AssignedToId == currentUserId ||
                (
                    task.VisibilityScope == TaskVisibilityScopes.TeamPublic &&
                    (task.AssignedToId == null || task.IsAssigneeVisibleToOthers) &&
                    (
                        (currentTeamId.HasValue && task.TeamId.HasValue && task.TeamId == currentTeamId) ||
                        isSubscriptionOwner
                    )
                ) ||
                (
                    task.VisibilityScope == TaskVisibilityScopes.GlobalPublic &&
                    (task.AssignedToId == null || task.IsAssigneeVisibleToOthers)
                ))
            {
                context.Succeed(requirement);
            }
        }
    }
}
