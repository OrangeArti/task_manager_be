using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos;
using TaskManager.Api.Models;

namespace TaskManager.Api.Services
{
    public class TeamService : ITeamService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<TeamService> _logger;

        public TeamService(ApplicationDbContext dbContext, ILogger<TeamService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<IReadOnlyList<TeamDto>> GetAllAsync()
        {
            var teams = await _dbContext.Teams
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new TeamDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    MemberCount = t.Members.Count
                })
                .ToListAsync();

            return teams;
        }

        public async Task<TeamDto?> GetByIdAsync(int id)
        {
            var team = await _dbContext.Teams
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new TeamDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    MemberCount = t.Members.Count
                })
                .FirstOrDefaultAsync();

            return team;
        }

        public async Task<TeamDto> CreateAsync(CreateTeamDto dto)
        {
            var entity = new Team
            {
                Name = dto.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Teams.Add(entity);
            await _dbContext.SaveChangesAsync();

            return new TeamDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                MemberCount = 0
            };
        }

        public async Task<TeamDto?> UpdateAsync(int id, UpdateTeamDto dto)
        {
            var entity = await _dbContext.Teams
                .Include(t => t.Members)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (entity is null)
            {
                _logger.LogInformation("Attempted to update team {TeamId} but it does not exist", id);
                return null;
            }

            if (dto.Name is { } name)
            {
                entity.Name = name.Trim();
            }

            if (dto.Description is { } description)
            {
                entity.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            }

            await _dbContext.SaveChangesAsync();

            return new TeamDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                MemberCount = entity.Members.Count
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _dbContext.Teams.FirstOrDefaultAsync(t => t.Id == id);

            if (entity is null)
            {
                _logger.LogInformation("Attempted to delete team {TeamId} but it does not exist", id);
                return false;
            }

            _dbContext.Teams.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<bool> AddMemberAsync(int teamId, string userId)
        {
            var team = await _dbContext.Teams
                .Include(t => t.Members)
                .FirstOrDefaultAsync(t => t.Id == teamId);

            if (team is null)
            {
                return false;
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
            {
                return false;
            }

            if (user.TeamId.HasValue && user.TeamId != teamId)
            {
                throw new InvalidOperationException("User already belongs to another team.");
            }

            user.TeamId = teamId;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("User {UserId} added to team {TeamId}", userId, teamId);

            return true;
        }

        public async Task<bool> RemoveMemberAsync(int teamId, string userId)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TeamId == teamId);

            if (user is null)
            {
                return false;
            }

            user.TeamId = null;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("User {UserId} removed from team {TeamId}", userId, teamId);

            return true;
        }

        public async Task<IReadOnlyList<UserDto>> GetMembersAsync(int teamId)
        {
            var members = await _dbContext.Users
                .Where(u => u.TeamId == teamId)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    DisplayName = u.DisplayName ?? string.Empty
                })
                .ToListAsync();

            return members;
        }
    }
}
