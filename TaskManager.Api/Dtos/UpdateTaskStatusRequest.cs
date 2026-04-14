using System.ComponentModel.DataAnnotations;
using TaskManager.Api.Models;

namespace TaskManager.Api.Dtos
{
    /// <summary>
    /// Update task status using the new Status enum values.
    /// Replaces the old IsCompleted bool shape (per D-03).
    /// </summary>
    public sealed class UpdateTaskStatusRequest
    {
        /// <summary>
        /// New task status: "Todo", "InProgress", or "Done".
        /// Required. Case-sensitive.
        /// </summary>
        [Required]
        public string? Status { get; set; }
    }
}
