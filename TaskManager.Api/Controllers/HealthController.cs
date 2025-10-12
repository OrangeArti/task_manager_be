using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Shared.Health;

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
        /// Единый health-check эндпоинт сервиса.
        /// </summary>
        /// <returns>Стандартный контракт состояния сервиса.</returns>
        /// <response code="200">Сервис и подключение к БД доступны.</response>
        /// <response code="503">Сервис недоступен или возникла ошибка подключения.</response>
        [HttpGet]
        [ProducesResponseType(typeof(HealthStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(HealthStatusDto), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<HealthStatusDto>> CheckHealth()
        {
            var response = new HealthStatusDto
            {
                Service = "tasks-core",
                CheckedAtUtc = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>()
            };

            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                if (canConnect)
                {
                    response.Status = "Healthy";
                    response.Details = "Application and database are reachable.";
                    response.Metadata!["database"] = "connected";
                    return Ok(response);
                }

                response.Status = "Unhealthy";
                response.Details = "Unable to connect to the database.";
                response.Metadata!["database"] = "unreachable";
                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
            }
            catch (Exception ex)
            {
                response.Status = "Unhealthy";
                response.Details = ex.Message;
                response.Metadata!["database"] = "error";
                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
            }
        }
    }
}
