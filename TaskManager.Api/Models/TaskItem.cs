using System;
using System.Collections.Generic;

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
        public bool IsAssigneeVisibleToOthers { get; set; } = true;

        public string VisibilityScope { get; set; } = TaskVisibilityScopes.Private;

        public string CreatedById { get; set; } = default!;
        public ApplicationUser? CreatedBy { get; set; }

        public string? AssignedToId { get; set; }
        public ApplicationUser? AssignedTo { get; set; }

        public int? GroupId { get; set; }
        public Group? Group { get; set; }

        public bool IsProblem { get; set; } = false;
        public string? ProblemDescription { get; set; }
        public string? ProblemReporterId { get; set; }
        public DateTime? ProblemReportedAt { get; set; }

        public string? FinishedByUserId { get; set; }
        public ApplicationUser? FinishedByUser { get; set; }

        public string? CompletionComment { get; set; }
    }

    public static class TaskVisibilityScopes
    {
        public const string Private = "Private";
        public const string TeamPublic = "TeamPublic";
        public const string GlobalPublic = "GlobalPublic";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Private,
                TeamPublic,
                GlobalPublic
            };

        public static string? Normalize(string? scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
                return null;

            foreach (var value in All)
            {
                if (string.Equals(value, scope, StringComparison.OrdinalIgnoreCase))
                    return value;
            }

            return null;
        }
    }
}
