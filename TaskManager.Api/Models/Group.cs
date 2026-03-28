namespace TaskManager.Api.Models;

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrganizationId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public Organization? Organization { get; set; }
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
