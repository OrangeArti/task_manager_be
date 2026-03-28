namespace TaskManager.Api.Models;

public class Subscription
{
    public int Id { get; set; }
    public string PlanType { get; set; } = "Free";
    public int OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Organization? Organization { get; set; }
}
