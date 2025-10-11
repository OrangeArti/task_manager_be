using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Models
{
    public class Team
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ApplicationUser> Members { get; set; } = new List<ApplicationUser>();
    }
}
