using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.Dtos.Teams;

public class TeamUpsertDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }
}
