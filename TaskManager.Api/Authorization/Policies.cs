using Microsoft.AspNetCore.Authorization;

namespace TaskManager.Api
{
    public static class Policies
    {
        public const string Admin = "AdminPolicy";
        public const string User = "UserPolicy";

        public static void RegisterPolicies(AuthorizationOptions options)
        {
            options.AddPolicy(Admin, policy => policy.RequireRole("Admin"));
            options.AddPolicy(User, policy => policy.RequireRole("User"));
        }
    }
}