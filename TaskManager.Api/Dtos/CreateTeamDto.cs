using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Dtos
{
    public sealed class CreateTeamDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; init; } = string.Empty;

        public string? Description { get; init; }
    }
}
