using Microsoft.AspNetCore.Identity;

namespace TaskManager.Api.Models
{
    // Extend IdentityUser to later attach profile info
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }

        public int? TeamId { get; set; }
        public Team? Team { get; set; }

        public string? SubscriptionId { get; set; }
    }
}
