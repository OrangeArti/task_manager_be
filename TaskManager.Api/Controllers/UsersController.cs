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
    [Authorize] // позже ограничим ролью/политикой
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

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { message = $"User '{id}' not found" });

            // Одна транзакция: обработка задач + удаление пользователя
            await using var tx = await _db.Database.BeginTransactionAsync();

            // 1) Забираем все задачи пользователя
            var tasks = await _db.Tasks.Where(t => t.OwnerId == id).ToListAsync();

            // 2) Делим на личные и публичные
            var personal = tasks.Where(t => !t.IsPublic).ToList();
            var publicOnes = tasks.Where(t => t.IsPublic).ToList();

            // 3) Личные — удаляем
            if (personal.Count > 0)
                _db.Tasks.RemoveRange(personal);

            // 4) Публичные — оставляем, но «обезличиваем»
            if (publicOnes.Count > 0)
            {
                foreach (var t in publicOnes)
                    t.OwnerId = null;

                _db.Tasks.UpdateRange(publicOnes);
            }

            await _db.SaveChangesAsync();

            // 5) Удаляем пользователя через Identity
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                await tx.RollbackAsync();
                var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                return Problem(detail: errors, statusCode: StatusCodes.Status500InternalServerError);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return NoContent();
        }
    }
}