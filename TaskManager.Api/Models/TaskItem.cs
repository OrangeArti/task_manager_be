namespace TaskManager.Api.Models
{
    public class TaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; } = false;
        public int Priority { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsPublic { get; set; } = false;
        public string? OwnerId { get; set; }
        public ApplicationUser? Owner { get; set; }
        public bool IsProblem { get; set; } = false;
        public string? ProblemDescription { get; set; }
        public string? ProblemReporterId { get; set; }
        public DateTime? ProblemReportedAt { get; set; }
    }
}