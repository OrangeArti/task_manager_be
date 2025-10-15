using System.ComponentModel.DataAnnotations;
using TaskManager.Api.Models;

namespace TaskManager.Api.Dtos
{
    /// <summary>
    /// Данные, разрешённые при создании задачи.
    /// </summary>
    public sealed class CreateTaskRequest
    {
        /// <summary>
        /// Заголовок задачи. Обязателен, 1..200 символов.
        /// </summary>
        [Required, StringLength(200, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Описание (необязательно, до 4000 символов).
        /// </summary>
        [StringLength(4000)]
        public string? Description { get; set; }

        /// <summary>
        /// Дедлайн (необязателен). Ожидается в UTC.
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Приоритет: 0 = Low, 1 = Medium, 2 = High.
        /// </summary>
        [Range(0, 2)]
        public int Priority { get; set; } = 0;

        /// <summary>Кому назначена задача.</summary>
        public string? AssignedToId { get; set; }

        /// <summary>Определяет, виден ли исполнитель всем (true) или только автору и исполнителю (false).</summary>
        public bool? IsAssigneeVisibleToOthers { get; set; }

        /// <summary>Команда задачи (для TeamPublic задач, опционально для Private).</summary>
        public int? TeamId { get; set; }

        /// <summary>Область видимости: Private | TeamPublic | GlobalPublic.</summary>
        [StringLength(32)]
        public string? VisibilityScope { get; set; } = TaskVisibilityScopes.Private;
    }
}
