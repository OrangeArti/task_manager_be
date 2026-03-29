namespace TaskManager.Api.Models;

public class OrgInvitation
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string InviteeEmail { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }

    public Organization? Organization { get; set; }
}
