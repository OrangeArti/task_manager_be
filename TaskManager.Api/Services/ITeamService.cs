using System.Collections.Generic;
using TaskManager.Api.Dtos;

namespace TaskManager.Api.Services
{
    public interface ITeamService
    {
        Task<IReadOnlyList<TeamDto>> GetAllAsync();

        Task<TeamDto?> GetByIdAsync(int id);

        Task<TeamDto> CreateAsync(CreateTeamDto dto);

        Task<TeamDto?> UpdateAsync(int id, UpdateTeamDto dto);

        Task<bool> DeleteAsync(int id);

        Task<bool> AddMemberAsync(int teamId, string userId);

        Task<bool> RemoveMemberAsync(int teamId, string userId);

        Task<IReadOnlyList<UserDto>> GetMembersAsync(int teamId);
    }
}
