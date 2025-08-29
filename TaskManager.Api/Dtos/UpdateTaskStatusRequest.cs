using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Dtos
{
    /// <summary>
    /// Обновление только статуса задачи.
    /// </summary>
    public sealed class UpdateTaskStatusRequest
    {
        /// <summary>
        /// Новый статус задачи: true = завершена, false = активна.
        /// Обязательное поле.
        /// </summary>
        [Required]
        public bool? IsCompleted { get; set; }
    }
}