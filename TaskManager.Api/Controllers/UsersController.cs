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
    [Authorize(Policy = Policies.Admin)] // Administer user management
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        /// <summary>Пагинированный список пользователей с поиском по email/displayName</summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<UserSummaryDto>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(u =>
                    (u.Email != null && u.Email.Contains(s)) ||
                    (u.UserName != null && u.UserName.Contains(s)) ||
                    (u.DisplayName != null && u.DisplayName.Contains(s)));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserSummaryDto
                {
                    Id = u.Id,
                    Email = u.Email ?? "",
                    DisplayName = u.DisplayName,
                    EmailConfirmed = u.EmailConfirmed
                })
                .ToListAsync();

            var result = new PagedResult<UserSummaryDto>
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = items
            };

            return Ok(result);
        }

        /// <summary>Удалить пользователя по Id. Личные задачи удаляются, публичные остаются без владельца.</summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            // safeguard: нельзя удалить самого себя
            var me = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            if (me == id)
                return BadRequest("Нельзя удалить самого себя.");

            // Выполним всё в рамках ExecutionStrategy + транзакции
            var strategy = _db.Database.CreateExecutionStrategy();

            IActionResult? resultToReturn = null; // захватим результат из лямбды

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                var user = await _userManager.FindByIdAsync(id);
                if (user is null)
                {
                    // ничего не меняли — просто выходим
                    resultToReturn = NotFound(new { message = $"User '{id}' not found" });
                    await tx.RollbackAsync();
                    return;
                }

                // 1) Забираем все задачи, созданные пользователем
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

                // 4) Сбрасываем назначение у задач, созданных другими, но назначенных на пользователя
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

                // 5) Удаляем пользователя через Identity
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

            // сюда придём уже после ExecuteAsync
            return resultToReturn ?? NoContent();
        }
    }
}
