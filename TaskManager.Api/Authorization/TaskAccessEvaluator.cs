using TaskManager.Api.Models;

namespace TaskManager.Api.Authorization
{
    internal static class TaskAccessEvaluator
    {
        internal readonly record struct TaskAccessSnapshot(
            string CreatedById,
            string? AssignedToId,
            bool IsAssigneeVisibleToOthers,
            string VisibilityScope,
            int? TeamId,
            string? ProblemReporterId,
            bool IsProblem
        );

        internal static TaskAccessSnapshot FromTask(TaskItem task) =>
            new(
                task.CreatedById,
                task.AssignedToId,
                task.IsAssigneeVisibleToOthers,
                task.VisibilityScope,
                task.TeamId,
                task.ProblemReporterId,
                task.IsProblem
            );

        private static bool IsPrivateAssignment(TaskAccessSnapshot task) =>
            task.AssignedToId is not null && !task.IsAssigneeVisibleToOthers;

        private static bool IsSameTeam(int? userTeamId, TaskAccessSnapshot task) =>
            userTeamId.HasValue && task.TeamId.HasValue && userTeamId.Value == task.TeamId.Value;

        internal static bool CanEditStatus(TaskAccessSnapshot task, string currentUserId, bool isAdmin, bool isSubscriptionOwner, int? userTeamId)
        {
            if (isAdmin) return true;
            if (task.CreatedById == currentUserId) return true;
            if (task.AssignedToId == currentUserId) return true;

            if (IsPrivateAssignment(task))
                return false;

            return task.VisibilityScope switch
            {
                TaskVisibilityScopes.Private => false,
                TaskVisibilityScopes.TeamPublic => IsSameTeam(userTeamId, task) || isSubscriptionOwner,
                TaskVisibilityScopes.GlobalPublic => true,
                _ => false
            };
        }

        internal static bool CanEditTask(TaskAccessSnapshot task, string currentUserId, bool isAdmin, bool isSubscriptionOwner, bool isTeamLead, int? userTeamId)
        {
            if (isAdmin) return true;

            var isOwner = task.CreatedById == currentUserId;

            return task.VisibilityScope switch
            {
                TaskVisibilityScopes.Private => isOwner,
                TaskVisibilityScopes.TeamPublic => isOwner ||
                                                   (isTeamLead && IsSameTeam(userTeamId, task)) ||
                                                   isSubscriptionOwner,
                TaskVisibilityScopes.GlobalPublic => isOwner || isSubscriptionOwner,
                _ => false
            };
        }

        internal static bool CanDeleteTask(TaskAccessSnapshot task, string currentUserId, bool isAdmin, bool isSubscriptionOwner, bool isTeamLead, int? userTeamId)
        {
            if (isAdmin) return true;

            var isOwner = task.CreatedById == currentUserId;

            return task.VisibilityScope switch
            {
                TaskVisibilityScopes.Private => isOwner,
                TaskVisibilityScopes.TeamPublic => isOwner ||
                                                   (isTeamLead && IsSameTeam(userTeamId, task)) ||
                                                   isSubscriptionOwner,
                TaskVisibilityScopes.GlobalPublic => isOwner || isSubscriptionOwner,
                _ => false
            };
        }

        internal static bool CanMarkProblem(TaskAccessSnapshot task, string currentUserId, bool isAdmin, bool isSubscriptionOwner, bool isTeamLead, int? userTeamId)
        {
            if (isAdmin) return true;

            if (task.VisibilityScope == TaskVisibilityScopes.Private)
                return task.CreatedById == currentUserId;

            if (task.CreatedById == currentUserId || task.AssignedToId == currentUserId)
                return true;

            if (IsPrivateAssignment(task))
                return false;

            return task.VisibilityScope switch
            {
                TaskVisibilityScopes.TeamPublic => IsSameTeam(userTeamId, task) || isSubscriptionOwner,
                TaskVisibilityScopes.GlobalPublic => true,
                _ => false
            };
        }

        internal static bool CanUnmarkProblem(TaskAccessSnapshot task, string currentUserId, bool isAdmin, bool isSubscriptionOwner, bool isTeamLead, int? userTeamId)
        {
            if (isAdmin) return true;
            if (!task.IsProblem) return true;

            if (task.VisibilityScope == TaskVisibilityScopes.Private)
            {
                if (task.ProblemReporterId == currentUserId)
                    return true;

                if ((isTeamLead || isSubscriptionOwner) && task.CreatedById == currentUserId)
                    return true;

                return false;
            }

            if (task.ProblemReporterId == currentUserId)
                return true;

            if (IsPrivateAssignment(task))
                return task.CreatedById == currentUserId;

            return task.VisibilityScope switch
            {
                TaskVisibilityScopes.TeamPublic => (isTeamLead && IsSameTeam(userTeamId, task)) || isSubscriptionOwner,
                TaskVisibilityScopes.GlobalPublic => isSubscriptionOwner,
                _ => false
            };
        }
    }
}
