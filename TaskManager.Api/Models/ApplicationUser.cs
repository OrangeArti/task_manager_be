using Microsoft.AspNetCore.Identity;

namespace TaskManager.Api.Models
{
    // Расширяем IdentityUser, чтобы позже добавить Profile info
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
    }
}