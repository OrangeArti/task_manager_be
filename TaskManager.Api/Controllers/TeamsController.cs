using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Api;
using TaskManager.Api.Dtos;
using TaskManager.Api.Services;
using TaskManager.Core.Dtos.Teams;

namespace TaskManager.Api.Controllers
{
    /// <summary>
    /// Team management: list, create, update, delete, and manage members.
    /// </summary>
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

        /// <summary>Returns all teams.</summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<TeamDto>>> GetAllAsync()
        {
            var teams = await _teamService.GetAllAsync();
            return Ok(teams);
        }

        /// <summary>Returns a team by its identifier.</summary>
        [HttpGet("by-id/{id:int}", Name = nameof(GetByIdAsync))]
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

        /// <summary>Creates a new team.</summary>
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

        /// <summary>Updates team info.</summary>
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

        /// <summary>Deletes a team.</summary>
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

        /// <summary>Adds a user to the team.</summary>
        [Authorize(Policy = Policies.TeamLeadOrAdmin)]
        [HttpPost("{teamId:int}/members")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddMember(int teamId, [FromBody] AddMemberRequest request)
        {
            var result = await _teamService.AddMemberAsync(teamId, request.UserId);
            if (!result)
            {
                return NotFound();
            }

            return Ok(new { message = "Member added successfully" });
        }

        /// <summary>Removes a user from the team.</summary>
        [Authorize(Policy = Policies.TeamLeadOrAdmin)]
        [HttpDelete("{teamId:int}/members")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveMember(int teamId, [FromBody] RemoveMemberRequest request)
        {
            var result = await _teamService.RemoveMemberAsync(teamId, request.UserId);
            if (!result)
            {
                return NotFound();
            }

            return Ok(new { message = "Member removed successfully" });
        }

        /// <summary>Returns the list of team members.</summary>
        [Authorize(Policy = Policies.TeamLeadOrAdmin)]
        [HttpGet("{teamId:int}/members")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMembers(int teamId)
        {
            var members = await _teamService.GetMembersAsync(teamId);
            return Ok(members);
        }
    }
}
