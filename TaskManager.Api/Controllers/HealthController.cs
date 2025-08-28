using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;

namespace TaskManager.Api.Controllers
{
    /// <summary>
    /// Health-check эндпоинты сервиса.
    /// </summary>
    [ApiController]
    [Route("health")]
    [Produces("application/json")]
    public class HealthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public HealthController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Проверка доступности подключения к базе данных.
        /// </summary>
        /// <returns>Статус здоровья и состояние подключения к БД.</returns>
        /// <response code="200">Подключение к БД доступно.</response>
        /// <response code="500">Подключение к БД недоступно или произошла ошибка.</response>
        [HttpGet("db")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CheckDatabase()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                if (canConnect)
                    return Ok(new { status = "Healthy", database = "Connected" });

                return StatusCode(500, new { status = "Unhealthy", database = "Cannot connect" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "Unhealthy", error = ex.Message });
            }
        }
    }
}