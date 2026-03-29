namespace TaskManager.Api.Models;

public class OrgMember
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }

    public Organization? Organization { get; set; }
    public ApplicationUser? User { get; set; }
}
