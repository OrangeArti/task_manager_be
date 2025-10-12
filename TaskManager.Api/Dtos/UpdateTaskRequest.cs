using System.ComponentModel.DataAnnotations;
using TaskManager.Api.Models;

namespace TaskManager.Api.Dtos
{
    /// <summary>
    /// Полное обновление редактируемых полей задачи.
    /// Статус меняется отдельным PATCH /status.
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

        /// <summary>Кому назначена задача.</summary>
        public string? AssignedToId { get; set; }

        /// <summary>Команда задачи.</summary>
        public int? TeamId { get; set; }

        /// <summary>Область видимости: Private | TeamPublic | GlobalPublic.</summary>
        [StringLength(32)]
        public string? VisibilityScope { get; set; }
    }
}
