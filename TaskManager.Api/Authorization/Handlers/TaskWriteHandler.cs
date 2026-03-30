using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Authorization.Requirements;
using TaskManager.Api.Data;
using TaskManager.Api.Authorization;

namespace TaskManager.Api.Authorization.Handlers
{
    /// <summary>
    /// Checks write permission for task endpoints according to role/scope matrix.
    /// </summary>
    public sealed class TaskWriteHandler : AuthorizationHandler<TaskWriteRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _dbContext;

        public TaskWriteHandler(IHttpContextAccessor httpContextAccessor, ApplicationDbContext dbContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _dbContext = dbContext;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TaskWriteRequirement requirement)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return;
            }

            var currentUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(currentUserId))
            {
                return;
            }

            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
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

            var task = await _dbContext.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task is null)
            {
                return;
            }

            var access = TaskAccessEvaluator.FromTask(task);

            var isTeamLead = context.User.IsInRole("TeamLead");

            var groupIdList = await _dbContext.GroupMembers
                .AsNoTracking()
                .Where(gm => gm.UserId == currentUserId)
                .Select(gm => gm.GroupId)
                .ToListAsync();
            IReadOnlySet<int> userGroupIds = new HashSet<int>(groupIdList);

            var isSubscriptionOwner = await _dbContext.OrgMembers
                .AnyAsync(m => m.UserId == currentUserId && m.Role == OrgRoles.SubscriptionOwner);

            var method = httpContext.Request.Method;
            var path = httpContext.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

            var isPatch = HttpMethods.IsPatch(method);
            var isPut = HttpMethods.IsPut(method);
            var isDelete = HttpMethods.IsDelete(method);

            var isStatusPatch = isPatch && path.EndsWith("/status", StringComparison.Ordinal);
            var isProblemPatch = isPatch && path.EndsWith("/problem", StringComparison.Ordinal);
            var isProblemDelete = isDelete && path.EndsWith("/problem", StringComparison.Ordinal);
            var isTaskDelete = isDelete && !isProblemDelete;

            var allowed = false;

            if (isPut)
            {
                allowed = TaskAccessEvaluator.CanEditTask(access, currentUserId, isAdmin: false, isSubscriptionOwner, isTeamLead, userGroupIds);
            }
            else if (isStatusPatch)
            {
                allowed = TaskAccessEvaluator.CanEditStatus(access, currentUserId, isAdmin: false, isSubscriptionOwner, userGroupIds);
            }
            else if (isProblemPatch)
            {
                allowed = TaskAccessEvaluator.CanMarkProblem(access, currentUserId, isAdmin: false, isSubscriptionOwner, isTeamLead, userGroupIds);
            }
            else if (isProblemDelete)
            {
                allowed = TaskAccessEvaluator.CanUnmarkProblem(access, currentUserId, isAdmin: false, isSubscriptionOwner, isTeamLead, userGroupIds);
            }
            else if (isTaskDelete)
            {
                allowed = TaskAccessEvaluator.CanDeleteTask(access, currentUserId, isAdmin: false, isSubscriptionOwner, isTeamLead, userGroupIds);
            }
            else
            {
                // Fallback: allow if user is creator
                allowed = access.CreatedById == currentUserId;
            }

            if (allowed)
            {
                context.Succeed(requirement);
            }
        }
    }
}
