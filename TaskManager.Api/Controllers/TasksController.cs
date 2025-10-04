using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos;
using TaskManager.Api.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace TaskManager.Api.Controllers
{
    /// <summary>
    /// CRUD по задачам: список, создание, изменение статуса, получение по id.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = Policies.User)] // позже ограничим ролью/политикой
    [Produces("application/json")]
    public class TasksController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public TasksController(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Возвращает идентификатор текущего пользователя из клеймов (NameIdentifier или sub).
        /// </summary>
        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
        }

        /// <summary>
        /// Возвращает список задач с пагинацией и сортировкой.
        /// </summary>
        /// <remarks>
        /// Пример: /api/tasks?page=1&amp;pageSize=20&amp;sortBy=dueDate&amp;sortDir=asc
        /// Поля сортировки: createdAt | dueDate | priority | title.
        /// </remarks>
        /// <response code="200">Список задач успешно получен.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<TaskItemDto>>> GetAll([FromQuery] TaskListQuery query)
        {
            var (page, pageSize) = query.NormalizePaging();
            var (sortBy, desc) = query.NormalizeSorting();
            var search = query.NormalizeSearch();

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var isAdmin = User.IsInRole("Admin");

            // 2) Базовый запрос: только задачи текущего пользователя
            var q = _db.Tasks
                .AsNoTracking();
            if (!isAdmin)
                q = q.Where(t => t.OwnerId == userId || t.IsPublic); // показываем публичные задачи

            // --- ФИЛЬТРЫ ---
            if (query.IsCompleted.HasValue)
                q = q.Where(t => t.IsCompleted == query.IsCompleted.Value);

            if (query.Priority.HasValue)
                q = q.Where(t => t.Priority == query.Priority.Value);

            if (query.IsPublic.HasValue)
                q = q.Where(t => t.IsPublic == query.IsPublic.Value);

            if (search is not null)
            {
                // простая case-insensitive contains (для MSSQL: переводим к одному регистру)
                var s = search.ToLower();
                q = q.Where(t =>
                    (t.Title != null && t.Title.ToLower().Contains(s)) ||
                    (t.Description != null && t.Description.ToLower().Contains(s)));
            }

            if (query.DueDateFrom.HasValue)
                q = q.Where(t => t.DueDate >= query.DueDateFrom.Value);

            if (query.DueDateTo.HasValue)
                q = q.Where(t => t.DueDate <= query.DueDateTo.Value);


            // Сортировка (whitelist полей)
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

            // Считаем total до Skip/Take (выполнится как COUNT(*) на стороне БД)
            var total = await q.CountAsync();

            // Пагинация
            var skip = (page - 1) * pageSize;

            var items = await q
                .Skip(skip)
                .Take(pageSize)
                .Select(t => new TaskItemDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    DueDate = t.DueDate,
                    IsCompleted = t.IsCompleted,
                    Priority = t.Priority,
                    CreatedAt = t.CreatedAt,
                    IsPublic = t.IsPublic,
                    IsProblem = t.IsProblem,
                    ProblemDescription = t.ProblemDescription,
                    ProblemReporterId = t.ProblemReporterId,
                    ProblemReportedAt = t.ProblemReportedAt
                })
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
        /// Создаёт новую задачу.
        /// </summary>
        /// <response code="201">Задача создана, возвращает объект и Location.</response>
        /// <response code="400">Данные не прошли валидацию.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TaskItemDto>> Create([FromBody] CreateTaskRequest request)
        {
            // Доп. доменная проверка (поверх аннотаций): дедлайн не в прошлом.
            if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError(nameof(request.DueDate), "DueDate cannot be in the past.");
                return ValidationProblem(ModelState);
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var entity = new TaskItem
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                DueDate = request.DueDate,
                Priority = request.Priority,
                // IsCompleted и CreatedAt проставятся по умолчанию конфигурацией/моделью
                OwnerId = userId,
                IsPublic = request.IsPublic ?? false
            };

            _db.Add(entity);
            await _db.SaveChangesAsync();

            var dto = new TaskItemDto
            {
                Id = entity.Id,
                Title = entity.Title,
                Description = entity.Description,
                DueDate = entity.DueDate,
                IsCompleted = entity.IsCompleted,
                Priority = entity.Priority,
                CreatedAt = entity.CreatedAt,
                IsPublic = entity.IsPublic,
                IsProblem = entity.IsProblem,
                ProblemDescription = entity.ProblemDescription,
                ProblemReporterId = entity.ProblemReporterId,
                ProblemReportedAt = entity.ProblemReportedAt
            };

            return CreatedAtRoute(
                routeName: "GetTaskById",
                routeValues: new { id = entity.Id },
                value: dto
            );
        }

        /// <summary>
        /// Возвращает задачу по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор задачи.</param>
        /// <response code="200">Задача найдена.</response>
        /// <response code="404">Задача не найдена.</response>
        [HttpGet("{id:int}", Name = "GetTaskById")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<TaskItemDto>> GetById([FromRoute] int id)
        {
            // берём текущего пользователя (двойной способ)
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            var isAdmin = User.IsInRole("Admin");

            // один запрос: фильтруем и по Id, и по владельцу
            var item = await _db.Tasks
                .AsNoTracking()
                .Where(t => t.Id == id && (isAdmin || t.OwnerId == currentUserId || t.IsPublic))
                .Select(t => new TaskItemDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    DueDate = t.DueDate,
                    IsCompleted = t.IsCompleted,
                    Priority = t.Priority,
                    CreatedAt = t.CreatedAt,
                    IsPublic = t.IsPublic,
                    IsProblem = t.IsProblem,
                    ProblemDescription = t.ProblemDescription,
                    ProblemReporterId = t.ProblemReporterId,
                    ProblemReportedAt = t.ProblemReportedAt
                })
                .FirstOrDefaultAsync();

            if (item is null)
                return NotFound(new { message = $"Task #{id} not found" });

            return Ok(item);
        }

        /// <summary>
        /// Обновляет статус задачи (выполнена/нет).
        /// </summary>
        /// <param name="id">Идентификатор задачи.</param>
        /// <param name="request">Тело запроса с новым значением статуса (<c>IsCompleted</c>).</param>
        /// <response code="200">Статус изменён, возвращаем обновлённый объект.</response>
        /// <response code="204">Статус уже имел переданное значение (идемпотентно, без изменений).</response>
        /// <response code="400">Некорректный запрос (нет поля IsCompleted или неверный формат).</response>
        /// <response code="404">Задача не найдена.</response>
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

            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && entity.OwnerId != currentUserId) return Forbid();


            var newValue = request.IsCompleted!.Value;

            if (entity.IsCompleted == newValue)
                return NoContent(); // идемпотентный ответ — ничего не изменили

            entity.IsCompleted = newValue;
            await _db.SaveChangesAsync();

            var dto = new TaskItemDto
            {
                Id = entity.Id,
                Title = entity.Title,
                Description = entity.Description,
                DueDate = entity.DueDate,
                IsCompleted = entity.IsCompleted,
                Priority = entity.Priority,
                CreatedAt = entity.CreatedAt,
                IsPublic = entity.IsPublic,
                IsProblem = entity.IsProblem,
                ProblemDescription = entity.ProblemDescription,
                ProblemReporterId = entity.ProblemReporterId,
                ProblemReportedAt = entity.ProblemReportedAt
            };

            return Ok(dto);
        }

        /// <summary>
        /// Полностью обновляет редактируемые поля задачи (без статуса).
        /// </summary>
        /// <param name="id">Идентификатор задачи.</param>
        /// <param name="request">Тело запроса с новыми значениями полей (Title, Description, DueDate, Priority).</param>
        /// <response code="200">Задача обновлена, возвращаем актуальный объект.</response>
        /// <response code="204">Данные совпадают с текущими — изменений нет.</response>
        /// <response code="400">Данные не прошли валидацию.</response>
        /// <response code="404">Задача не найдена.</response>
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

            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && entity.OwnerId != currentUserId) return Forbid();

            // Доменная проверка: дедлайн не в прошлом
            if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError(nameof(request.DueDate), "DueDate cannot be in the past.");
                return ValidationProblem(ModelState);
            }

            // Нормализуем вход
            var newTitle = request.Title.Trim();
            var newDescription = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();

            // Грязевая проверка (ничего не меняем — возвращаем 204)
            var noChanges =
                entity.Title == newTitle &&
                entity.Description == newDescription &&
                entity.DueDate == request.DueDate &&
                entity.Priority == request.Priority &&
                (request.IsPublic == null || entity.IsPublic == request.IsPublic.Value);

            if (noChanges)
                return NoContent();

            // Применяем изменения
            entity.Title = newTitle;
            entity.Description = newDescription;
            entity.DueDate = request.DueDate;
            entity.Priority = request.Priority;
            if (request.IsPublic.HasValue)
                entity.IsPublic = request.IsPublic.Value;

            await _db.SaveChangesAsync();

            var dto = new TaskItemDto
            {
                Id = entity.Id,
                Title = entity.Title,
                Description = entity.Description,
                DueDate = entity.DueDate,
                IsCompleted = entity.IsCompleted, // статус трогает отдельный PATCH /status
                Priority = entity.Priority,
                CreatedAt = entity.CreatedAt,
                IsPublic = entity.IsPublic,
                IsProblem = entity.IsProblem,
                ProblemDescription = entity.ProblemDescription,
                ProblemReporterId = entity.ProblemReporterId,
                ProblemReportedAt = entity.ProblemReportedAt
            };

            return Ok(dto);
        }

        /// <summary>
        /// Удаляет задачу по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор задачи.</param>
        /// <response code="204">Задача успешно удалена.</response>
        /// <response code="404">Задача не найдена.</response>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            var entity = await _db.Tasks.FindAsync(id);
            if (entity is null)
                return NotFound(new { message = $"Task #{id} not found" });

            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && entity.OwnerId != currentUserId) return Forbid();

            _db.Tasks.Remove(entity);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Пометить задачу как проблемную (с описанием).
        /// </summary>
        /// <response code="200">Задача помечена как проблемная (возвращает обновлённый объект).</response>
        /// <response code="204">Состояние уже было таким же (идемпотентно, без изменений).</response>
        /// <response code="400">Некорректное тело запроса.</response>
        /// <response code="401">Не авторизован.</response>
        /// <response code="403">Нет прав на изменение (не владелец).</response>
        /// <response code="404">Задача не найдена.</response>
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

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            var entity = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (entity is null)
                return NotFound(new { message = $"Task #{id} not found" });

            if (entity.OwnerId != currentUserId)
                return Forbid();

            var newDescription = request.Description.Trim();

            // идемпотентность: уже проблемная с тем же текстом
            if (entity.IsProblem && string.Equals(entity.ProblemDescription ?? "", newDescription, StringComparison.Ordinal))
                return NoContent();

            entity.IsProblem = true;
            entity.ProblemDescription = newDescription;
            entity.ProblemReporterId = currentUserId;
            entity.ProblemReportedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var dto = new TaskItemDto
            {
                Id = entity.Id,
                Title = entity.Title,
                Description = entity.Description,
                DueDate = entity.DueDate,
                IsCompleted = entity.IsCompleted,
                Priority = entity.Priority,
                CreatedAt = entity.CreatedAt,
                IsPublic = entity.IsPublic,

                IsProblem = entity.IsProblem,
                ProblemDescription = entity.ProblemDescription,
                ProblemReporterId = entity.ProblemReporterId,
                ProblemReportedAt = entity.ProblemReportedAt
            };

            return Ok(dto);
        }

        /// <summary>
        /// Снять пометку «проблемная» с задачи.
        /// </summary>
        /// <response code="200">Проблема снята (возвращает обновлённый объект).</response>
        /// <response code="204">Задача уже была без проблемы.</response>
        /// <response code="401">Не авторизован.</response>
        /// <response code="403">Нет прав на изменение (не владелец).</response>
        /// <response code="404">Задача не найдена.</response>
        [HttpDelete("{id:int}/problem")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UnmarkProblem([FromRoute] int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            var entity = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (entity is null)
                return NotFound(new { message = $"Task #{id} not found" });

            if (entity.OwnerId != currentUserId)
                return Forbid();

            if (!entity.IsProblem)
                return NoContent();

            entity.IsProblem = false;
            entity.ProblemDescription = null;
            entity.ProblemReporterId = null;
            entity.ProblemReportedAt = null;

            await _db.SaveChangesAsync();

            var dto = new TaskItemDto
            {
                Id = entity.Id,
                Title = entity.Title,
                Description = entity.Description,
                DueDate = entity.DueDate,
                IsCompleted = entity.IsCompleted,
                Priority = entity.Priority,
                CreatedAt = entity.CreatedAt,
                IsPublic = entity.IsPublic,

                IsProblem = entity.IsProblem,
                ProblemDescription = entity.ProblemDescription,
                ProblemReporterId = entity.ProblemReporterId,
                ProblemReportedAt = entity.ProblemReportedAt
            };

            return Ok(dto);
        }
    }
}
