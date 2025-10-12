using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.Dtos.Teams;

public class TeamDto
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    [Range(0, int.MaxValue)]
    public int MemberCount { get; set; }
}
