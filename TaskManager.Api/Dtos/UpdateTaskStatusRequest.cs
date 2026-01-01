using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Dtos
{
    /// <summary>
    /// Update only the task status.
    /// </summary>
    public sealed class UpdateTaskStatusRequest
    {
        /// <summary>
        /// New task status: true = completed, false = active.
        /// Required field.
        /// </summary>
        [Required]
        public bool? IsCompleted { get; set; }

        public string? CompletionComment { get; set; }
    }
}
