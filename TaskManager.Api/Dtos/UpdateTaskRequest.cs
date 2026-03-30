using System.ComponentModel.DataAnnotations;
using TaskManager.Api.Models;

namespace TaskManager.Api.Dtos
{
    /// <summary>
    /// Full update of editable task fields.
    /// Status is changed separately via PATCH /status.
    /// </summary>
    public sealed class UpdateTaskRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public DateTime? DueDate { get; set; }

        [Range(0, 2)]
        public int Priority { get; set; } = 0;

        /// <summary>Assignee for the task.</summary>
        public string? AssignedToId { get; set; }

        /// <summary>Whether the assignee is visible to other users.</summary>
        public bool? IsAssigneeVisibleToOthers { get; set; }

        /// <summary>Group for the task (required for TeamPublic tasks).</summary>
        public int? GroupId { get; set; }

        /// <summary>Visibility scope: Private | TeamPublic | GlobalPublic.</summary>
        [StringLength(32)]
        public string? VisibilityScope { get; set; }
    }
}
