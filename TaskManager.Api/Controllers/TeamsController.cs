using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Api;
using TaskManager.Api.Dtos;
using TaskManager.Api.Services;

namespace TaskManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = Policies.User)]
    [Produces("application/json")]
    public class TeamsController : ControllerBase
    {
        private readonly ITeamService _teamService;

        public TeamsController(ITeamService teamService)
        {
            _teamService = teamService;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<TeamDto>>> GetAllAsync()
        {
            var teams = await _teamService.GetAllAsync();
            return Ok(teams);
        }

        [HttpGet("{id:int}", Name = nameof(GetByIdAsync))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TeamDto>> GetByIdAsync(int id)
        {
            var team = await _teamService.GetByIdAsync(id);
            if (team is null)
            {
                return NotFound();
            }

            return Ok(team);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TeamDto>> CreateAsync([FromBody] CreateTeamDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                ModelState.AddModelError(nameof(dto.Name), "Team name cannot be empty.");
                return ValidationProblem(ModelState);
            }

            var createdTeam = await _teamService.CreateAsync(dto);

            return CreatedAtRoute(nameof(GetByIdAsync), new { id = createdTeam.Id }, createdTeam);
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TeamDto>> UpdateAsync(int id, [FromBody] UpdateTeamDto dto)
        {
            if (dto.Name is null && dto.Description is null)
            {
                ModelState.AddModelError(string.Empty, "At least one field must be provided to update a team.");
                return ValidationProblem(ModelState);
            }

            if (dto.Name is { } name && string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError(nameof(dto.Name), "Team name cannot be empty.");
                return ValidationProblem(ModelState);
            }

            var updatedTeam = await _teamService.UpdateAsync(id, dto);
            if (updatedTeam is null)
            {
                return NotFound();
            }

            return Ok(updatedTeam);
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAsync(int id)
        {
            var deleted = await _teamService.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
