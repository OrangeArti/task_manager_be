using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Dtos
{
    public sealed class UpdateTeamDto
    {
        [StringLength(100)]
        public string? Name { get; init; }

        public string? Description { get; init; }
    }
}
