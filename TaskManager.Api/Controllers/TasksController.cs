using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos;

namespace TaskManager.Api.Controllers
{
    /// <summary>
    /// CRUD по задачам. В этом микро‑шаге реализуем только список (GET).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TasksController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public TasksController(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Возвращает список задач (все записи) в виде DTO.
        /// </summary>
        /// <remarks>
        /// MVP‑версия: без пагинации и фильтров. Далее добавим.
        /// </remarks>
        /// <response code="200">Список задач успешно получен.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<TaskItemDto>>> GetAll()
        {
            // IQueryable -> проекция в DTO -> исполнение на стороне БД
            var items = await _db.Tasks
                .AsNoTracking() // читаем без отслеживания (чуть быстрее, меньше памяти)
                .Select(t => new TaskItemDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    DueDate = t.DueDate,
                    IsCompleted = t.IsCompleted,
                    Priority = t.Priority,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return Ok(items);
        }
    }
}