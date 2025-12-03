using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Shared.Health;

namespace TaskManager.Api.Controllers
{
    /// <summary>
    /// Health-check endpoints for the service.
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
        /// Single health-check endpoint for the service.
        /// </summary>
        /// <returns>Standard service health contract.</returns>
        /// <response code="200">Service and database connection are available.</response>
        /// <response code="503">Service unavailable or database connection error.</response>
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
