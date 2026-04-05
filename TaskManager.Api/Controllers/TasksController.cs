using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos;
using TaskManager.Api.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Linq.Expressions;
using TaskManager.Api.Authorization;

namespace TaskManager.Api.Controllers
{
    /// <summary>
    /// CRUD for tasks with team-based visibility: Admins see all tasks; users see own, team, and public tasks.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = Policies.User)] // may tighten role/policy later
    [Produces("application/json")]
    public class TasksController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public TasksController(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Looks up the ApplicationUser.Id by matching the Keycloak sub claim
        /// against the KeycloakSubject bridge column. Returns null if not found.
        /// </summary>
        private async Task<string?> GetCurrentUserDbIdAsync()
        {
            // JWT middleware maps the 'sub' claim to ClaimTypes.NameIdentifier
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (sub is null) return null;

            var dbId = await _db.Users
                .Where(u => u.KeycloakSubject == sub)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (dbId is not null) return dbId;

            // JIT provisioning: create a local Identity row on first Keycloak login
            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? $"{sub}@keycloak";
            var user = new ApplicationUser
            {
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                KeycloakSubject = sub,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user.Id;
        }

        private static readonly Expression<Func<TaskItem, TaskItemDto>> TaskToDtoProjection = t => new TaskItemDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            DueDate = t.DueDate,
            IsCompleted = t.IsCompleted,
            Priority = t.Priority,
            CreatedAt = t.CreatedAt,
            VisibilityScope = t.VisibilityScope,
            CreatedById = t.CreatedById,
            AssignedToId = t.AssignedToId,
            IsAssigneeVisibleToOthers = t.IsAssigneeVisibleToOthers,
            IsProblem = t.IsProblem,
            ProblemDescription = t.ProblemDescription,
            ProblemReporterId = t.ProblemReporterId,
            ProblemReportedAt = t.ProblemReportedAt,
            FinishedByUserId = t.FinishedByUserId,
            CompletionComment = t.CompletionComment
        };

        private static TaskItemDto ToDto(TaskItem entity) => new TaskItemDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            DueDate = entity.DueDate,
            IsCompleted = entity.IsCompleted,
            Priority = entity.Priority,
            CreatedAt = entity.CreatedAt,
            VisibilityScope = entity.VisibilityScope,
            CreatedById = entity.CreatedById,
            AssignedToId = entity.AssignedToId,
            IsAssigneeVisibleToOthers = entity.IsAssigneeVisibleToOthers,
            IsProblem = entity.IsProblem,
            ProblemDescription = entity.ProblemDescription,
            ProblemReporterId = entity.ProblemReporterId,
            ProblemReportedAt = entity.ProblemReportedAt,
            FinishedByUserId = entity.FinishedByUserId,
            CompletionComment = entity.CompletionComment
        };


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

        /// <summary>
        /// Returns a paginated, sorted list of tasks.
        /// </summary>
        /// <remarks>
        /// Example: /api/tasks?page=1&amp;pageSize=20&amp;sortBy=dueDate&amp;sortDir=asc
        /// Sortable fields: createdAt | dueDate | priority | title.
        /// </remarks>
        /// <response code="200">Task list fetched successfully.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<TaskItemDto>>> GetAll([FromQuery] TaskListQuery query)
        {
            var (page, pageSize) = query.NormalizePaging();
            var (sortBy, desc) = query.NormalizeSorting();
            var search = query.NormalizeSearch();
            var requestedScope = TaskVisibilityScopes.Normalize(query.NormalizeVisibilityScope());

            var userId = await GetCurrentUserDbIdAsync();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            var (userGroupIds, isSubscriptionOwner) = await GetUserGroupContextAsync(userId);

            var q = _db.Tasks.AsNoTracking();

            if (!isAdmin)
            {
                var groupIdsList = userGroupIds.ToList();
                q = q.Where(t =>
                    t.CreatedById == userId ||
                    t.AssignedToId == userId ||
                    (
                        t.VisibilityScope == TaskVisibilityScopes.TeamPublic &&
                        (t.AssignedToId == null || t.IsAssigneeVisibleToOthers) &&
                        (
                            (t.GroupId.HasValue && groupIdsList.Contains(t.GroupId.Value)) ||
                            isSubscriptionOwner
                        )
                    ) ||
                    (
                        t.VisibilityScope == TaskVisibilityScopes.GlobalPublic &&
                        (t.AssignedToId == null || t.IsAssigneeVisibleToOthers)
                    ));
            }

            if (query.IsCompleted.HasValue)
                q = q.Where(t => t.IsCompleted == query.IsCompleted.Value);

            if (query.Priority.HasValue)
                q = q.Where(t => t.Priority == query.Priority.Value);

            if (!string.IsNullOrEmpty(requestedScope))
                q = q.Where(t => t.VisibilityScope == requestedScope);

            if (search is not null)
            {
                var s = search.ToLower();
                q = q.Where(t =>
                    (t.Title != null && t.Title.ToLower().Contains(s)) ||
                    (t.Description != null && t.Description.ToLower().Contains(s)));
            }

            if (query.DueDateFrom.HasValue)
                q = q.Where(t => t.DueDate >= query.DueDateFrom.Value);

            if (query.DueDateTo.HasValue)
                q = q.Where(t => t.DueDate <= query.DueDateTo.Value);

            q = (sortBy, desc) switch
            {
                ("createdAt", false) => q.OrderBy(t => t.CreatedAt),
                ("createdAt", true) => q.OrderByDescending(t => t.CreatedAt),
                ("dueDate", false) => q.OrderBy(t => t.DueDate),
                ("dueDate", true) => q.OrderByDescending(t => t.DueDate),
                ("priority", false) => q.OrderBy(t => t.Priority),
                ("priority", true) => q.OrderByDescending(t => t.Priority),
                ("title", false) => q.OrderBy(t => t.Title),
                ("title", true) => q.OrderByDescending(t => t.Title),
                _ => q.OrderByDescending(t => t.CreatedAt)
            };

            var total = await q.CountAsync();
            var skip = (page - 1) * pageSize;

            var items = await q
                .Skip(skip)
                .Take(pageSize)
                .Select(TaskToDtoProjection)
                .ToListAsync();

            var result = new PagedResult<TaskItemDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };

            return Ok(result);
        }

        /// <summary>
        /// Creates a new task.
        /// </summary>
        /// <response code="201">Task created; returns object and Location.</response>
        /// <response code="400">Validation failed.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskItemDto>> Create([FromBody] CreateTaskRequest request)
        {
            if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError(nameof(request.DueDate), "DueDate cannot be in the past.");
                return ValidationProblem(ModelState);
            }

            var userId = await GetCurrentUserDbIdAsync();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var normalizedScope = TaskVisibilityScopes.Normalize(request.VisibilityScope);
            if (request.VisibilityScope is not null && normalizedScope is null)
            {
                ModelState.AddModelError(nameof(request.VisibilityScope), "Unknown visibility scope.");
                return ValidationProblem(ModelState);
            }

            var effectiveScope = normalizedScope ?? TaskVisibilityScopes.Private;

            var isAdmin = User.IsInRole("Admin");
            var isTeamLead = User.IsInRole("TeamLead");
            var (userGroupIds, isSubscriptionOwner) = await GetUserGroupContextAsync(userId);
            var canChangeAssigneeVisibility = isAdmin || isSubscriptionOwner || isTeamLead;

            int? groupId = request.GroupId;

            if (effectiveScope == TaskVisibilityScopes.TeamPublic)
            {
                if (!groupId.HasValue)
                {
                    ModelState.AddModelError(nameof(request.GroupId), "TeamPublic tasks require a group.");
                    return ValidationProblem(ModelState);
                }

                var groupIdValue = groupId.Value;

                if (!isAdmin && !isSubscriptionOwner)
                {
                    if (!userGroupIds.Contains(groupIdValue))
                    {
                        return Forbid();
                    }
                }

                var groupExists = await _db.Groups.AnyAsync(g => g.Id == groupIdValue);
                if (!groupExists)
                {
                    ModelState.AddModelError(nameof(request.GroupId), $"Group '{groupIdValue}' not found.");
                    return ValidationProblem(ModelState);
                }
            }
            else
            {
                groupId = null;
            }

            var assignedToId = string.IsNullOrWhiteSpace(request.AssignedToId)
                ? null
                : request.AssignedToId.Trim();

            if (effectiveScope == TaskVisibilityScopes.Private && assignedToId is not null)
            {
                ModelState.AddModelError(nameof(request.AssignedToId), "Private tasks cannot have an assignee.");
                return ValidationProblem(ModelState);
            }

            if (assignedToId is null && request.IsAssigneeVisibleToOthers.HasValue && request.IsAssigneeVisibleToOthers.Value == false)
            {
                ModelState.AddModelError(nameof(request.IsAssigneeVisibleToOthers), "IsAssigneeVisibleToOthers can be set to false only when the task is assigned.");
                return ValidationProblem(ModelState);
            }

            bool isAssigneeVisibleToOthers = true;

            if (assignedToId is not null)
            {
                var assigneeExists = await _db.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == assignedToId);

                if (!assigneeExists)
                {
                    ModelState.AddModelError(nameof(request.AssignedToId), "Assigned user not found.");
                    return ValidationProblem(ModelState);
                }

                if (!canChangeAssigneeVisibility &&
                    request.IsAssigneeVisibleToOthers.HasValue &&
                    request.IsAssigneeVisibleToOthers.Value == false)
                {
                    return Forbid();
                }

                isAssigneeVisibleToOthers = request.IsAssigneeVisibleToOthers ?? true;
            }

            var entity = new TaskItem
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                DueDate = request.DueDate,
                Priority = request.Priority,
                AssignedToId = assignedToId,
                IsAssigneeVisibleToOthers = isAssigneeVisibleToOthers,
                GroupId = groupId,
                VisibilityScope = effectiveScope,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Add(entity);
            await _db.SaveChangesAsync();

            var dto = ToDto(entity);

            return CreatedAtRoute(
                routeName: "GetTaskById",
                routeValues: new { id = entity.Id },
                value: dto
            );
        }

        /// <summary>
        /// Returns a task by identifier.
        /// </summary>
        /// <param name="id">Task identifier.</param>
        /// <response code="200">Task found.</response>
        /// <response code="404">Task not found.</response>
        [HttpGet("{id:int}", Name = "GetTaskById")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<TaskItemDto>> GetById([FromRoute] int id)
        {
            // obtain current user id (two-claim lookup)
            var currentUserId = await GetCurrentUserDbIdAsync();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            var (userGroupIds, isSubscriptionOwner) = await GetUserGroupContextAsync(currentUserId);

            var itemQuery = _db.Tasks.AsNoTracking().Where(t => t.Id == id);

            if (!isAdmin)
            {
                var groupIdsList = userGroupIds.ToList();
                itemQuery = itemQuery.Where(t =>
                    t.CreatedById == currentUserId ||
                    t.AssignedToId == currentUserId ||
                    (
                        t.VisibilityScope == TaskVisibilityScopes.TeamPublic &&
                        (t.AssignedToId == null || t.IsAssigneeVisibleToOthers) &&
                        (
                            (t.GroupId.HasValue && groupIdsList.Contains(t.GroupId.Value)) ||
                            isSubscriptionOwner
                        )
                    ) ||
                    (
                        t.VisibilityScope == TaskVisibilityScopes.GlobalPublic &&
                        (t.AssignedToId == null || t.IsAssigneeVisibleToOthers)
                    ));
            }

            var item = await itemQuery
                .Select(TaskToDtoProjection)
                .FirstOrDefaultAsync();

            if (item is null)
                return NotFound(new { message = $"Task #{id} not found" });

            return Ok(item);
        }

        /// <summary>
        /// Updates a task status (complete/incomplete).
        /// </summary>
        /// <param name="id">Task identifier.</param>
        /// <param name="request">Request body with the new <c>IsCompleted</c> value.</param>
        /// <response code="200">Status changed; returns updated object.</response>
        /// <response code="204">Status already had the provided value (idempotent, no changes).</response>
        /// <response code="400">Invalid request (missing IsCompleted or wrong format).</response>
        /// <response code="404">Task not found.</response>
        [HttpPatch("{id:int}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateStatus([FromRoute] int id, [FromBody] UpdateTaskStatusRequest request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var entity = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (entity is null)
                return NotFound(new { message = $"Task #{id} not found" });

            var currentUserId = await GetCurrentUserDbIdAsync();
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            var (userGroupIds, isSubscriptionOwner) = await GetUserGroupContextAsync(currentUserId);
            var access = TaskAccessEvaluator.FromTask(entity);

            if (!TaskAccessEvaluator.CanEditStatus(access, currentUserId, isAdmin, isSubscriptionOwner, userGroupIds))
                return Forbid();

            var newValue = request.IsCompleted!.Value;

            if (entity.IsCompleted == newValue)
                return NoContent(); // idempotent response — nothing changed

            entity.IsCompleted = newValue;

            if (newValue)
            {
                entity.FinishedByUserId = currentUserId;
                entity.CompletionComment = request.CompletionComment?.Trim();
            }
            else
            {
                entity.FinishedByUserId = null;
                entity.CompletionComment = null;
            }

            await _db.SaveChangesAsync();

            return Ok(ToDto(entity));
        }

        /// <summary>
        /// Fully updates editable task fields (excluding status).
        /// </summary>
        /// <param name="id">Task identifier.</param>
        /// <param name="request">Request body with new values (Title, Description, DueDate, Priority).</param>
        /// <response code="200">Task updated; returns the current object.</response>
        /// <response code="204">Data matches current values — no changes.</response>
        /// <response code="400">Validation failed.</response>
        /// <response code="404">Task not found.</response>
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdateTaskRequest request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var entity = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (entity is null)
                return NotFound(new { message = $"Task #{id} not found" });

            var currentUserId = await GetCurrentUserDbIdAsync();
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            var isTeamLead = User.IsInRole("TeamLead");
            var (userGroupIds, isSubscriptionOwner) = await GetUserGroupContextAsync(currentUserId);
            var canChangeAssigneeVisibility = isAdmin || isSubscriptionOwner || isTeamLead;
            var access = TaskAccessEvaluator.FromTask(entity);
            var isOwner = access.CreatedById == currentUserId;

            if (!TaskAccessEvaluator.CanEditTask(access, currentUserId, isAdmin, isSubscriptionOwner, isTeamLead, userGroupIds))
                return Forbid();

            if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError(nameof(request.DueDate), "DueDate cannot be in the past.");
                return ValidationProblem(ModelState);
            }

            var newTitle = request.Title.Trim();
            var newDescription = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();

            var newAssignedToId = string.IsNullOrWhiteSpace(request.AssignedToId)
                ? null
                : request.AssignedToId.Trim();

            var scopeFromRequest = request.VisibilityScope is null
                ? entity.VisibilityScope
                : TaskVisibilityScopes.Normalize(request.VisibilityScope);

            if (request.VisibilityScope is not null && scopeFromRequest is null)
            {
                ModelState.AddModelError(nameof(request.VisibilityScope), "Unknown visibility scope.");
                return ValidationProblem(ModelState);
            }

            var targetScope = scopeFromRequest!;
            var scopeChanged = targetScope != entity.VisibilityScope;

            int? newGroupId = request.GroupId;

            if (targetScope == TaskVisibilityScopes.TeamPublic)
            {
                if (!newGroupId.HasValue)
                {
                    if (!scopeChanged && entity.GroupId.HasValue)
                    {
                        newGroupId = entity.GroupId;
                    }
                }

                if (!newGroupId.HasValue)
                {
                    ModelState.AddModelError(nameof(request.GroupId), "TeamPublic tasks require a group.");
                    return ValidationProblem(ModelState);
                }

                var newGroupIdValue = newGroupId.Value;

                if (!isAdmin && !isSubscriptionOwner)
                {
                    if (!userGroupIds.Contains(newGroupIdValue))
                    {
                        return Forbid();
                    }
                }

                var groupExists = await _db.Groups.AnyAsync(g => g.Id == newGroupIdValue);
                if (!groupExists)
                {
                    ModelState.AddModelError(nameof(request.GroupId), $"Group '{newGroupIdValue}' not found.");
                    return ValidationProblem(ModelState);
                }
            }
            else
            {
                newGroupId = null;
            }

            if (!isAdmin)
            {
                var inGroupForUpdate = newGroupId.HasValue && userGroupIds.Contains(newGroupId.Value);
                var targetScopeAllowed = targetScope switch
                {
                    TaskVisibilityScopes.Private => isOwner,
                    TaskVisibilityScopes.TeamPublic => isSubscriptionOwner || (inGroupForUpdate && (isTeamLead || isOwner)),
                    TaskVisibilityScopes.GlobalPublic => isSubscriptionOwner || isOwner,
                    _ => false
                };

                if (!targetScopeAllowed)
                    return Forbid();
            }

            if (targetScope == TaskVisibilityScopes.Private && newAssignedToId is not null)
            {
                ModelState.AddModelError(nameof(request.AssignedToId), "Private tasks cannot have an assignee.");
                return ValidationProblem(ModelState);
            }

            if (newAssignedToId is null &&
                request.IsAssigneeVisibleToOthers.HasValue &&
                request.IsAssigneeVisibleToOthers.Value == false)
            {
                ModelState.AddModelError(nameof(request.IsAssigneeVisibleToOthers), "IsAssigneeVisibleToOthers can be set to false only when the task is assigned.");
                return ValidationProblem(ModelState);
            }

            bool newIsAssigneeVisible = entity.IsAssigneeVisibleToOthers;

            if (request.IsAssigneeVisibleToOthers.HasValue)
            {
                var requestedVisibility = request.IsAssigneeVisibleToOthers.Value;
                if (requestedVisibility != entity.IsAssigneeVisibleToOthers && !canChangeAssigneeVisibility)
                {
                    return Forbid();
                }

                newIsAssigneeVisible = requestedVisibility;
            }

            if (newAssignedToId is not null)
            {
                var assigneeExists = await _db.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == newAssignedToId);

                if (!assigneeExists)
                {
                    ModelState.AddModelError(nameof(request.AssignedToId), "Assigned user not found.");
                    return ValidationProblem(ModelState);
                }
            }
            else
            {
                newIsAssigneeVisible = true;
            }

            var noChanges =
                entity.Title == newTitle &&
                entity.Description == newDescription &&
                entity.DueDate == request.DueDate &&
                entity.Priority == request.Priority &&
                entity.VisibilityScope == targetScope &&
                entity.AssignedToId == newAssignedToId &&
                entity.GroupId == newGroupId &&
                entity.IsAssigneeVisibleToOthers == newIsAssigneeVisible;

            if (noChanges)
                return NoContent();

            entity.Title = newTitle;
            entity.Description = newDescription;
            entity.DueDate = request.DueDate;
            entity.Priority = request.Priority;
            entity.AssignedToId = newAssignedToId;
            entity.IsAssigneeVisibleToOthers = newIsAssigneeVisible;
            entity.VisibilityScope = targetScope;
            entity.GroupId = newGroupId;

            await _db.SaveChangesAsync();

            return Ok(ToDto(entity));
        }

        /// <summary>
        /// Deletes a task by identifier.
        /// </summary>
        /// <param name="id">Task identifier.</param>
        /// <response code="204">Task deleted successfully.</response>
        /// <response code="404">Task not found.</response>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            var entity = await _db.Tasks.FindAsync(id);
            if (entity is null)
                return NotFound(new { message = $"Task #{id} not found" });

            var currentUserId = await GetCurrentUserDbIdAsync();
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            var isTeamLead = User.IsInRole("TeamLead");
            var (userGroupIds, isSubscriptionOwner) = await GetUserGroupContextAsync(currentUserId);
            var access = TaskAccessEvaluator.FromTask(entity);

            if (!TaskAccessEvaluator.CanDeleteTask(access, currentUserId, isAdmin, isSubscriptionOwner, isTeamLead, userGroupIds))
                return Forbid();

            _db.Tasks.Remove(entity);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Mark a task as problematic (with description).
        /// </summary>
        /// <response code="200">Task marked as problematic (returns updated object).</response>
        /// <response code="204">State was already the same (idempotent, no changes).</response>
        /// <response code="400">Invalid request body.</response>
        /// <response code="401">Unauthorized.</response>
        /// <response code="403">Forbidden (not the owner).</response>
        /// <response code="404">Task not found.</response>
        [HttpPatch("{id:int}/problem")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkProblem([FromRoute] int id, [FromBody] MarkProblemRequest request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var currentUserId = await GetCurrentUserDbIdAsync();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            var entity = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (entity is null)
                return NotFound(new { message = $"Task #{id} not found" });

            var isAdmin = User.IsInRole("Admin");
            var isTeamLead = User.IsInRole("TeamLead");
            var (userGroupIds, isSubscriptionOwner) = await GetUserGroupContextAsync(currentUserId);
            var access = TaskAccessEvaluator.FromTask(entity);

            if (!TaskAccessEvaluator.CanMarkProblem(access, currentUserId, isAdmin, isSubscriptionOwner, isTeamLead, userGroupIds))
                return Forbid();

            var newDescription = request.Description.Trim();

            // idempotency: already marked problematic with the same text
            if (entity.IsProblem && string.Equals(entity.ProblemDescription ?? "", newDescription, StringComparison.Ordinal))
                return NoContent();

            entity.IsProblem = true;
            entity.ProblemDescription = newDescription;
            entity.ProblemReporterId = currentUserId;
            entity.ProblemReportedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(ToDto(entity));
        }

        /// <summary>
        /// Remove the "problematic" marker from a task.
        /// </summary>
        /// <response code="200">Problem cleared (returns updated object).</response>
        /// <response code="204">Task was already without a problem.</response>
        /// <response code="401">Unauthorized.</response>
        /// <response code="403">Forbidden (not the owner).</response>
        /// <response code="404">Task not found.</response>
        [HttpDelete("{id:int}/problem")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UnmarkProblem([FromRoute] int id)
        {
            var currentUserId = await GetCurrentUserDbIdAsync();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            var entity = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (entity is null)
                return NotFound(new { message = $"Task #{id} not found" });

            var isAdmin = User.IsInRole("Admin");
            var isTeamLead = User.IsInRole("TeamLead");
            var (userGroupIds, isSubscriptionOwner) = await GetUserGroupContextAsync(currentUserId);
            var access = TaskAccessEvaluator.FromTask(entity);

            if (!TaskAccessEvaluator.CanUnmarkProblem(access, currentUserId, isAdmin, isSubscriptionOwner, isTeamLead, userGroupIds))
                return Forbid();

            if (!entity.IsProblem)
                return NoContent();

            entity.IsProblem = false;
            entity.ProblemDescription = null;
            entity.ProblemReporterId = null;
            entity.ProblemReportedAt = null;

            await _db.SaveChangesAsync();

            return Ok(ToDto(entity));
        }

        /// <summary>
        /// Assigns the task to the current user (self-assignment).
        /// </summary>
        /// <remarks>
        /// Allows a user to claim a task that is currently unassigned (or assigned to themselves).
        /// Requires the task to be visible to the user.
        /// </remarks>
        /// <response code="200">Task assigned to self.</response>
        /// <response code="400">Task already assigned to someone else.</response>
        /// <response code="403">Task not visible or not allowed to claim.</response>
        /// <response code="404">Task not found.</response>
        [HttpPatch("{id:int}/assign-self")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignSelf([FromRoute] int id)
        {
            var currentUserId = await GetCurrentUserDbIdAsync();
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var entity = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (entity is null)
                return NotFound(new { message = $"Task #{id} not found" });

            // 1. Check visibility (same logic as GetById)
            var isAdmin = User.IsInRole("Admin");
            var (userGroupIds, isSubscriptionOwner) = await GetUserGroupContextAsync(currentUserId);

            if (!isAdmin)
            {
                // Basic visibility check
                bool isVisible =
                    entity.CreatedById == currentUserId ||
                    entity.AssignedToId == currentUserId ||
                     (
                        entity.VisibilityScope == TaskVisibilityScopes.TeamPublic &&
                        (
                            (entity.GroupId.HasValue && userGroupIds.Contains(entity.GroupId.Value)) ||
                            isSubscriptionOwner
                        )
                    ) ||
                    (
                        entity.VisibilityScope == TaskVisibilityScopes.GlobalPublic
                    );

                if (!isVisible)
                    return Forbid();
            }

            // 2. Assignment Logic
            // If already assigned to CURRENT USER -> Success (Idempotent)
            if (entity.AssignedToId == currentUserId)
            {
                return Ok(ToDto(entity));
            }

            // If assigned to SOMEONE ELSE -> Fail
            if (!string.IsNullOrEmpty(entity.AssignedToId))
            {
                return BadRequest(new { message = "Task is already assigned to another user." });
            }

            // If Unassigned -> Claim it
            entity.AssignedToId = currentUserId;
            // Ensure visibility of assignee is set to true by default for self-assignment, 
            // or keep existing logic. Let's set it to true as per general expectation.
            entity.IsAssigneeVisibleToOthers = true;

            await _db.SaveChangesAsync();

            return Ok(ToDto(entity));
        }
    }
}
