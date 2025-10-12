using Microsoft.AspNetCore.Authorization;

namespace TaskManager.Api.Authorization.Requirements
{
    /// <summary>
    /// Read access: Admin, author/assignee, subscription owner, or eligible team/public visibility.
    /// </summary>
    public sealed class TaskReadRequirement : IAuthorizationRequirement
    {
    }
}
