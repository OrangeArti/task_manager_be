using TaskManager.Api.Authorization;
using TaskManager.Api.Models;
using Xunit;

namespace TaskManager.Tests
{
    public class TaskAccessEvaluatorTests
    {
        [Fact]
        public void Private_Task_Should_Be_Editable_Only_By_Owner()
        {
            // arrange
            var task = new TaskItem
            {
                Id = 1,
                CreatedById = "user1",
                VisibilityScope = TaskVisibilityScopes.Private
            };

            var snapshot = TaskAccessEvaluator.FromTask(task);

            // act
            var canOwnerEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "user1",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userTeamId: null
            );

            var canOtherEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "user2",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userTeamId: null
            );

            // assert
            Assert.True(canOwnerEdit);
            Assert.False(canOtherEdit);
        }
        [Fact]
        public void TeamPublic_Privately_Assigned_Task_Should_Not_Be_Editable_By_Other_Team_Member()
        {
            // arrange
            var task = new TaskItem
            {
                Id = 2,
                CreatedById = "lead",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                TeamId = 1,
                AssignedToId = "member1",
                IsAssigneeVisibleToOthers = false // приватное назначение
            };

            var snapshot = TaskAccessEvaluator.FromTask(task);

            // тимлид команды
            var canTeamLeadEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "lead",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: true,
                userTeamId: 1
            );

            // исполнитель
            var canAssigneeEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "member1",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userTeamId: 1
            );

            // другой участник той же команды
            var canOtherMemberEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "member2",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userTeamId: 1
            );

            // assert
            Assert.True(canTeamLeadEdit);
            Assert.False(canAssigneeEdit);
            Assert.False(canOtherMemberEdit);
        }
        [Fact]
        public void GlobalPublic_Task_Should_Be_Editable_Only_By_SubscriptionOwner_Or_Admin()
        {
            // arrange
            var task = new TaskItem
            {
                Id = 3,
                CreatedById = "user1",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic,
                TeamId = 1
            };

            var snapshot = TaskAccessEvaluator.FromTask(task);

            // обычный пользователь
            var canRegularUserEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "user2",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userTeamId: 1
            );

            // тимлид
            var canTeamLeadEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "lead",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: true,
                userTeamId: 1
            );

            // владелец подписки
            var canSubscriptionOwnerEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "owner",
                isAdmin: false,
                isSubscriptionOwner: true,
                isTeamLead: false,
                userTeamId: null
            );

            // админ
            var canAdminEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "admin",
                isAdmin: true,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userTeamId: null
            );

            // assert
            Assert.False(canRegularUserEdit);
            Assert.False(canTeamLeadEdit);
            Assert.True(canSubscriptionOwnerEdit);
            Assert.True(canAdminEdit);
        }
        [Fact]
        public void Private_Task_Should_Be_Deletable_Only_By_Owner()
        {
            // arrange
            var task = new TaskItem
            {
                Id = 10,
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private
            };

            var snapshot = TaskAccessEvaluator.FromTask(task);

            // владелец
            var canOwnerDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "owner",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userTeamId: null
            );

            // другой обычный пользователь
            var canOtherUserDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "someone-else",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userTeamId: null
            );

            // assert
            Assert.True(canOwnerDelete);
            Assert.False(canOtherUserDelete);
        }
        [Fact]
        public void GlobalPublic_Task_Should_Be_Deletable_Only_By_SubscriptionOwner_Or_Admin()
        {
            // arrange
            var task = new TaskItem
            {
                Id = 11,
                CreatedById = "user1",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic,
                TeamId = 1
            };

            var snapshot = TaskAccessEvaluator.FromTask(task);

            // обычный пользователь
            var canRegularUserDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "user2",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userTeamId: 1
            );

            // тимлид
            var canTeamLeadDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "lead",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: true,
                userTeamId: 1
            );

            // владелец подписки
            var canSubscriptionOwnerDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "owner",
                isAdmin: false,
                isSubscriptionOwner: true,
                isTeamLead: false,
                userTeamId: null
            );

            // админ
            var canAdminDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "admin",
                isAdmin: true,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userTeamId: null
            );

            // assert
            Assert.False(canRegularUserDelete);
            Assert.False(canTeamLeadDelete);
            Assert.True(canSubscriptionOwnerDelete);
            Assert.True(canAdminDelete);
        }

        [Fact]
        public void CanEditStatus_Should_Respect_Roles_Scope_And_Assignee_Visibility()
        {
            var privateTask = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private,
                AssignedToId = "assignee",
                IsAssigneeVisibleToOthers = false
            });

            Assert.True(TaskAccessEvaluator.CanEditStatus(privateTask, "owner", false, false, 1));
            Assert.True(TaskAccessEvaluator.CanEditStatus(privateTask, "assignee", false, false, 1));
            Assert.False(TaskAccessEvaluator.CanEditStatus(privateTask, "outsider", false, false, 2));

            var teamPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                TeamId = 1
            });

            Assert.True(TaskAccessEvaluator.CanEditStatus(teamPublic, "member", false, false, 1)); // same team
            Assert.True(TaskAccessEvaluator.CanEditStatus(teamPublic, "sub-owner", false, true, 2)); // subscription owner
            Assert.False(TaskAccessEvaluator.CanEditStatus(teamPublic, "other", false, false, 2)); // foreign team

            var globalPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic
            });

            Assert.True(TaskAccessEvaluator.CanEditStatus(globalPublic, "anyone", false, false, null));
        }

        [Fact]
        public void CanEditTask_Should_Follow_Roles_And_Scopes()
        {
            var privateTask = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private
            });

            Assert.True(TaskAccessEvaluator.CanEditTask(privateTask, "owner", false, false, false, 1));
            Assert.False(TaskAccessEvaluator.CanEditTask(privateTask, "member", false, false, false, 1));

            var teamPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                TeamId = 1
            });

            Assert.True(TaskAccessEvaluator.CanEditTask(teamPublic, "owner", false, false, false, 1));
            Assert.True(TaskAccessEvaluator.CanEditTask(teamPublic, "lead", false, false, true, 1)); // team lead same team
            Assert.True(TaskAccessEvaluator.CanEditTask(teamPublic, "sub-owner", false, true, false, 2)); // subscription owner
            Assert.False(TaskAccessEvaluator.CanEditTask(teamPublic, "other", false, false, false, 2));

            var globalPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic
            });

            Assert.True(TaskAccessEvaluator.CanEditTask(globalPublic, "owner", false, false, false, null));
            Assert.True(TaskAccessEvaluator.CanEditTask(globalPublic, "sub-owner", false, true, false, null));
            Assert.False(TaskAccessEvaluator.CanEditTask(globalPublic, "user", false, false, false, null));
        }

        [Fact]
        public void CanDeleteTask_Should_Follow_Roles_And_Scopes()
        {
            var privateTask = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private
            });
            Assert.True(TaskAccessEvaluator.CanDeleteTask(privateTask, "owner", false, false, false, null));
            Assert.False(TaskAccessEvaluator.CanDeleteTask(privateTask, "other", false, false, false, null));

            var teamPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                TeamId = 1
            });
            Assert.True(TaskAccessEvaluator.CanDeleteTask(teamPublic, "lead", false, false, true, 1)); // team lead same team
            Assert.True(TaskAccessEvaluator.CanDeleteTask(teamPublic, "sub-owner", false, true, false, 2)); // subscription owner
            Assert.False(TaskAccessEvaluator.CanDeleteTask(teamPublic, "other", false, false, false, 2));

            var globalPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic
            });
            Assert.True(TaskAccessEvaluator.CanDeleteTask(globalPublic, "sub-owner", false, true, false, null));
            Assert.False(TaskAccessEvaluator.CanDeleteTask(globalPublic, "random", false, false, false, null));
        }

        [Fact]
        public void CanMarkProblem_Should_Handle_Private_Assignments_And_Scopes()
        {
            var privateTask = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private
            });
            Assert.True(TaskAccessEvaluator.CanMarkProblem(privateTask, "owner", false, false, false, null));
            Assert.False(TaskAccessEvaluator.CanMarkProblem(privateTask, "other", false, false, false, null));

            var privateAssignedHidden = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                AssignedToId = "assignee",
                IsAssigneeVisibleToOthers = false,
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                TeamId = 1
            });
            Assert.True(TaskAccessEvaluator.CanMarkProblem(privateAssignedHidden, "assignee", false, false, false, 1)); // assignee allowed
            Assert.False(TaskAccessEvaluator.CanMarkProblem(privateAssignedHidden, "member", false, false, false, 1)); // hidden assignment blocks others

            var teamPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                TeamId = 1
            });
            Assert.True(TaskAccessEvaluator.CanMarkProblem(teamPublic, "member", false, false, false, 1)); // same team
            Assert.True(TaskAccessEvaluator.CanMarkProblem(teamPublic, "sub-owner", false, true, false, 2));
            Assert.False(TaskAccessEvaluator.CanMarkProblem(teamPublic, "other", false, false, false, 2));

            var globalPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic
            });
            Assert.True(TaskAccessEvaluator.CanMarkProblem(globalPublic, "anyone", false, false, false, null));
        }

        [Fact]
        public void CanUnmarkProblem_Should_Follow_Roles_Assignment_And_Scope()
        {
            // not a problem -> always true
            var notProblem = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private,
                IsProblem = false
            });
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(notProblem, "any", false, false, false, null));

            var privateProblem = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private,
                IsProblem = true,
                ProblemReporterId = "reporter"
            });
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(privateProblem, "reporter", false, false, false, null));
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(privateProblem, "owner", false, true, false, null)); // subscription owner who is creator
            Assert.False(TaskAccessEvaluator.CanUnmarkProblem(privateProblem, "stranger", false, false, false, null));

            var teamPublicProblem = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                TeamId = 1,
                IsProblem = true,
                ProblemReporterId = "reporter"
            });
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(teamPublicProblem, "reporter", false, false, false, 2));
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(teamPublicProblem, "lead", false, false, true, 1)); // team lead same team
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(teamPublicProblem, "sub-owner", false, true, false, 2));
            Assert.False(TaskAccessEvaluator.CanUnmarkProblem(teamPublicProblem, "member2", false, false, false, 2)); // other team

            var globalProblem = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic,
                IsProblem = true,
                ProblemReporterId = "reporter"
            });
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(globalProblem, "reporter", false, false, false, null));
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(globalProblem, "sub-owner", false, true, false, null));
            Assert.False(TaskAccessEvaluator.CanUnmarkProblem(globalProblem, "random", false, false, false, null));

            var hiddenAssigneeProblem = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                AssignedToId = "assignee",
                IsAssigneeVisibleToOthers = false,
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                TeamId = 1,
                IsProblem = true,
                ProblemReporterId = "assignee"
            });
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(hiddenAssigneeProblem, "assignee", false, false, false, 1)); // reporter
            Assert.False(TaskAccessEvaluator.CanUnmarkProblem(hiddenAssigneeProblem, "member", false, false, false, 1)); // hidden assignee blocks others
        }
    }
}
