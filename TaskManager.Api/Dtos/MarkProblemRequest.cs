using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Dtos
{
    public class MarkProblemRequest
    {
        [Required]
        [StringLength(2000, MinimumLength = 3)]
        public string Description { get; set; } = "";
    }
}