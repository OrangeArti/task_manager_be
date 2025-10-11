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
    }
}
