using System.ComponentModel.DataAnnotations;

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
    }
}