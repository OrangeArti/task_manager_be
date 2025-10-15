using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.Dtos.Tasks;

public class TaskUpsertDto
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }

    [Range(0, 5)]
    public int Priority { get; set; }

    [StringLength(450)]
    public string? AssignedToId { get; set; }

    public bool IsAssigneeVisibleToOthers { get; set; } = true;

    public int? TeamId { get; set; }

    [StringLength(50)]
    public string VisibilityScope { get; set; } = "Private";
}
