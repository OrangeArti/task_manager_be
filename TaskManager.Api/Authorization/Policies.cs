using Microsoft.AspNetCore.Authorization;
using TaskManager.Api.Authorization.Requirements;

namespace TaskManager.Api
{
    public static class Policies
    {
        public const string Admin = "AdminPolicy";
        public const string SubscriptionOwner = "SubscriptionOwnerPolicy";
        public const string TeamLead = "TeamLeadPolicy";
        public const string User = "UserPolicy";
        public const string TeamLeadOrAdmin = "TeamLeadOrAdmin";
        public const string TaskReadAccess = "TaskReadAccess";
        public const string TaskWriteAccess = "TaskWriteAccess";

        public static void RegisterPolicies(AuthorizationOptions options)
        {
            options.AddPolicy(Admin, p => p.RequireRole("Admin"));
            options.AddPolicy(SubscriptionOwner, p => p.RequireRole("SubscriptionOwner", "Admin"));
            options.AddPolicy(TeamLead, p => p.RequireRole("TeamLead", "SubscriptionOwner", "Admin"));
            options.AddPolicy(User, p => p.RequireRole("User", "TeamLead", "SubscriptionOwner", "Admin"));
            options.AddPolicy(TeamLeadOrAdmin, policy => policy.RequireAssertion(ctx =>
                ctx.User.IsInRole("Admin") ||
                ctx.User.IsInRole("SubscriptionOwner") ||
                ctx.User.IsInRole("TeamLead")));
            options.AddPolicy(TaskReadAccess, policy => policy.AddRequirements(new TaskReadRequirement()));
            options.AddPolicy(TaskWriteAccess, policy => policy.AddRequirements(new TaskWriteRequirement()));
        }
    }
}
