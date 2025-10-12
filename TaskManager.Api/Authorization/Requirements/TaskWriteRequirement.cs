using Microsoft.AspNetCore.Authorization;

namespace TaskManager.Api.Authorization.Requirements
{
    /// <summary>
    /// Write access to a task: Admin or creator.
    /// </summary>
    public sealed class TaskWriteRequirement : IAuthorizationRequirement
    {
    }
}
