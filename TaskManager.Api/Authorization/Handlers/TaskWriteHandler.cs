using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Authorization.Requirements;
using TaskManager.Api.Data;

namespace TaskManager.Api.Authorization.Handlers
{
    /// <summary>
    /// Checks write permission for task (Admin or creator only).
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

            var isCreator = await _dbContext.Tasks
                .AsNoTracking()
                .AnyAsync(t => t.Id == taskId && t.CreatedById == currentUserId);

            if (isCreator)
            {
                context.Succeed(requirement);
            }
        }
    }
}
