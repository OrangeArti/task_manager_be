using System.ComponentModel.DataAnnotations;
using TaskManager.Api.Models;

namespace TaskManager.Api.Dtos
{
    /// <summary>
    /// Payload allowed when creating a task.
    /// </summary>
    public sealed class CreateTaskRequest
    {
        /// <summary>
        /// Task title. Required, 1..200 characters.
        /// </summary>
        [Required, StringLength(200, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Description (optional, up to 4000 characters).
        /// </summary>
        [StringLength(4000)]
        public string? Description { get; set; }

        /// <summary>
        /// Due date (optional). Expected in UTC.
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Priority: 0 = Low, 1 = Medium, 2 = High.
        /// </summary>
        [Range(0, 2)]
        public int Priority { get; set; } = 0;

        /// <summary>Assignee for the task.</summary>
        public string? AssignedToId { get; set; }

        /// <summary>Whether the assignee is visible to everyone (true) or only author and assignee (false).</summary>
        public bool? IsAssigneeVisibleToOthers { get; set; }

        /// <summary>Group for the task (required for TeamPublic tasks).</summary>
        public int? GroupId { get; set; }

        /// <summary>Visibility scope: Private | TeamPublic | GlobalPublic.</summary>
        [StringLength(32)]
        public string? VisibilityScope { get; set; } = TaskVisibilityScopes.Private;
    }
}
