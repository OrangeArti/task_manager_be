namespace TaskManager.Api.Models;

public class GroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }

    public Group? Group { get; set; }
    public ApplicationUser? User { get; set; }
}
