using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Health;
using TaskManager.Shared.Health;
using System.Diagnostics;

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
        private readonly IDatabaseHealthProbe _dbProbe;
        private readonly ILogger<HealthController> _logger;

        public HealthController(IDatabaseHealthProbe dbProbe, ILogger<HealthController> logger)
        {
            _dbProbe = dbProbe;
            _logger = logger;
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

            var traceId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();
            response.Metadata!["traceId"] = traceId;

            try
            {
                var probe = await _dbProbe.CheckAsync();

                if (!probe.CanConnect)
                {
                    response.Status = "Unhealthy";
                    response.Details = probe.Error ?? "Unable to connect to the database.";
                    response.Metadata!["database"] = "unreachable";
                    _logger.LogError("Health check failed: {Detail} (traceId={TraceId})", response.Details, traceId);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
                }

                response.Metadata!["database"] = "connected";
                response.Metadata!["pendingMigrations"] = probe.PendingMigrations.ToString();

                if (probe.PendingMigrations > 0)
                {
                    response.Status = "Degraded";
                    response.Details = "Database reachable but migrations are pending.";
                    _logger.LogWarning("Health degraded: {Detail} (traceId={TraceId}, pendingMigrations={Pending})",
                        response.Details, traceId, probe.PendingMigrations);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
                }

                response.Status = "Healthy";
                response.Details = "Application and database are reachable.";
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.Status = "Unhealthy";
                response.Details = ex.Message;
                response.Metadata!["database"] = "error";
                _logger.LogError(ex, "Health check threw an exception (traceId={TraceId})", traceId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
            }
        }
    }
}
