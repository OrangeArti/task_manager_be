using System.ComponentModel.DataAnnotations;

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

        /// <summary>
        /// Публичная задача или нет. По умолчанию false (задача приватная).
        /// </summary>
        public bool? IsPublic { get; set; }
    }
}