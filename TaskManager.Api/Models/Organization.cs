namespace TaskManager.Api.Models;

public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ApplicationUser? Owner { get; set; }
    public Subscription? Subscription { get; set; }
    public ICollection<Group> Groups { get; set; } = new List<Group>();
}
