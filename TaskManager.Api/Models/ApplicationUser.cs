using Microsoft.AspNetCore.Identity;

namespace TaskManager.Api.Models
{
    // Extend IdentityUser to later attach profile info
    public class ApplicationUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }

        public string? SubscriptionId { get; set; }

        /// <summary>
        /// The Keycloak sub (subject) UUID for this user.
        /// Used to look up the ApplicationUser from a Keycloak JWT.
        /// Nullable: existing users have no Keycloak subject until they log in via Keycloak.
        /// </summary>
        public string? KeycloakSubject { get; set; }
    }
}
