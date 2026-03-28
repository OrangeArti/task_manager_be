namespace TaskManager.Api.Models;

public class Comment
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public TaskItem? Task { get; set; }
    public ApplicationUser? Author { get; set; }
}
