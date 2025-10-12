using TaskManager.Api.Models;

namespace TaskManager.Api.Dtos
{
    /// <summary>
    /// Публичная форма задачи для отдачи во внешний мир (REST API).
    /// </summary>
    public sealed class TaskItemDto
    {
        public int Id { get; init; }
        public string Title { get; init; } = "";
        public string? Description { get; init; }
        public DateTime? DueDate { get; init; }
        public bool IsCompleted { get; init; }
        public int Priority { get; init; }
        public DateTime CreatedAt { get; init; }
        public string VisibilityScope { get; init; } = TaskVisibilityScopes.Private;
        public string CreatedById { get; init; } = string.Empty;
        public string? AssignedToId { get; init; }
        public int? TeamId { get; init; }
        public bool IsProblem { get; set; }
        public string? ProblemDescription { get; set; }
        public string? ProblemReporterId { get; set; }
        public DateTime? ProblemReportedAt { get; set; }
    }
}
