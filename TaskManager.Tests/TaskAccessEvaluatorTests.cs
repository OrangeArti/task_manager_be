using System.Collections.Generic;
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
                userGroupIds: new HashSet<int>()
            );

            var canOtherEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "user2",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userGroupIds: new HashSet<int>()
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
                GroupId = 1,
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
                userGroupIds: new HashSet<int> { 1 }
            );

            // исполнитель
            var canAssigneeEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "member1",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userGroupIds: new HashSet<int> { 1 }
            );

            // другой участник той же команды
            var canOtherMemberEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "member2",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userGroupIds: new HashSet<int> { 1 }
            );

            // assert
            Assert.True(canTeamLeadEdit);
            Assert.True(canAssigneeEdit); // assignee is in group, can edit
            Assert.True(canOtherMemberEdit); // group members can edit TeamPublic tasks
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
                GroupId = 1
            };

            var snapshot = TaskAccessEvaluator.FromTask(task);

            // обычный пользователь
            var canRegularUserEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "user2",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userGroupIds: new HashSet<int> { 1 }
            );

            // тимлид
            var canTeamLeadEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "lead",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: true,
                userGroupIds: new HashSet<int> { 1 }
            );

            // владелец подписки
            var canSubscriptionOwnerEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "owner",
                isAdmin: false,
                isSubscriptionOwner: true,
                isTeamLead: false,
                userGroupIds: new HashSet<int>()
            );

            // админ
            var canAdminEdit = TaskAccessEvaluator.CanEditTask(
                snapshot,
                currentUserId: "admin",
                isAdmin: true,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userGroupIds: new HashSet<int>()
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
                userGroupIds: new HashSet<int>()
            );

            // другой обычный пользователь
            var canOtherUserDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "someone-else",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userGroupIds: new HashSet<int>()
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
                GroupId = 1
            };

            var snapshot = TaskAccessEvaluator.FromTask(task);

            // обычный пользователь
            var canRegularUserDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "user2",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userGroupIds: new HashSet<int> { 1 }
            );

            // тимлид
            var canTeamLeadDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "lead",
                isAdmin: false,
                isSubscriptionOwner: false,
                isTeamLead: true,
                userGroupIds: new HashSet<int> { 1 }
            );

            // владелец подписки
            var canSubscriptionOwnerDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "owner",
                isAdmin: false,
                isSubscriptionOwner: true,
                isTeamLead: false,
                userGroupIds: new HashSet<int>()
            );

            // админ
            var canAdminDelete = TaskAccessEvaluator.CanDeleteTask(
                snapshot,
                currentUserId: "admin",
                isAdmin: true,
                isSubscriptionOwner: false,
                isTeamLead: false,
                userGroupIds: new HashSet<int>()
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

            Assert.True(TaskAccessEvaluator.CanEditStatus(privateTask, "owner", false, false, new HashSet<int> { 1 }));
            Assert.True(TaskAccessEvaluator.CanEditStatus(privateTask, "assignee", false, false, new HashSet<int> { 1 }));
            Assert.False(TaskAccessEvaluator.CanEditStatus(privateTask, "outsider", false, false, new HashSet<int> { 2 }));

            var teamPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                GroupId = 1
            });

            Assert.True(TaskAccessEvaluator.CanEditStatus(teamPublic, "member", false, false, new HashSet<int> { 1 })); // same group
            Assert.True(TaskAccessEvaluator.CanEditStatus(teamPublic, "sub-owner", false, true, new HashSet<int> { 2 })); // subscription owner
            Assert.False(TaskAccessEvaluator.CanEditStatus(teamPublic, "other", false, false, new HashSet<int> { 2 })); // foreign group

            var globalPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic
            });

            Assert.True(TaskAccessEvaluator.CanEditStatus(globalPublic, "anyone", false, false, new HashSet<int>()));
        }

        [Fact]
        public void CanEditTask_Should_Follow_Roles_And_Scopes()
        {
            var privateTask = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private
            });

            Assert.True(TaskAccessEvaluator.CanEditTask(privateTask, "owner", false, false, false, new HashSet<int> { 1 }));
            Assert.False(TaskAccessEvaluator.CanEditTask(privateTask, "member", false, false, false, new HashSet<int> { 1 }));

            var teamPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                GroupId = 1
            });

            Assert.True(TaskAccessEvaluator.CanEditTask(teamPublic, "owner", false, false, false, new HashSet<int> { 1 }));
            Assert.True(TaskAccessEvaluator.CanEditTask(teamPublic, "lead", false, false, true, new HashSet<int> { 1 })); // team lead same group
            Assert.True(TaskAccessEvaluator.CanEditTask(teamPublic, "sub-owner", false, true, false, new HashSet<int> { 2 })); // subscription owner
            Assert.False(TaskAccessEvaluator.CanEditTask(teamPublic, "other", false, false, false, new HashSet<int> { 2 }));

            var globalPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic
            });

            Assert.True(TaskAccessEvaluator.CanEditTask(globalPublic, "owner", false, false, false, new HashSet<int>()));
            Assert.True(TaskAccessEvaluator.CanEditTask(globalPublic, "sub-owner", false, true, false, new HashSet<int>()));
            Assert.False(TaskAccessEvaluator.CanEditTask(globalPublic, "user", false, false, false, new HashSet<int>()));
        }

        [Fact]
        public void CanDeleteTask_Should_Follow_Roles_And_Scopes()
        {
            var privateTask = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private
            });
            Assert.True(TaskAccessEvaluator.CanDeleteTask(privateTask, "owner", false, false, false, new HashSet<int>()));
            Assert.False(TaskAccessEvaluator.CanDeleteTask(privateTask, "other", false, false, false, new HashSet<int>()));

            var teamPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                GroupId = 1
            });
            Assert.True(TaskAccessEvaluator.CanDeleteTask(teamPublic, "lead", false, false, true, new HashSet<int> { 1 })); // team lead same group
            Assert.True(TaskAccessEvaluator.CanDeleteTask(teamPublic, "sub-owner", false, true, false, new HashSet<int> { 2 })); // subscription owner
            Assert.False(TaskAccessEvaluator.CanDeleteTask(teamPublic, "other", false, false, false, new HashSet<int> { 2 }));

            var globalPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic
            });
            Assert.True(TaskAccessEvaluator.CanDeleteTask(globalPublic, "sub-owner", false, true, false, new HashSet<int>()));
            Assert.False(TaskAccessEvaluator.CanDeleteTask(globalPublic, "random", false, false, false, new HashSet<int>()));
        }

        [Fact]
        public void CanMarkProblem_Should_Handle_Private_Assignments_And_Scopes()
        {
            var privateTask = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private
            });
            Assert.True(TaskAccessEvaluator.CanMarkProblem(privateTask, "owner", false, false, false, new HashSet<int>()));
            Assert.False(TaskAccessEvaluator.CanMarkProblem(privateTask, "other", false, false, false, new HashSet<int>()));

            var privateAssignedHidden = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                AssignedToId = "assignee",
                IsAssigneeVisibleToOthers = false,
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                GroupId = 1
            });
            Assert.True(TaskAccessEvaluator.CanMarkProblem(privateAssignedHidden, "assignee", false, false, false, new HashSet<int> { 1 })); // assignee allowed
            Assert.False(TaskAccessEvaluator.CanMarkProblem(privateAssignedHidden, "member", false, false, false, new HashSet<int> { 1 })); // hidden assignment blocks others

            var teamPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                GroupId = 1
            });
            Assert.True(TaskAccessEvaluator.CanMarkProblem(teamPublic, "member", false, false, false, new HashSet<int> { 1 })); // same group
            Assert.True(TaskAccessEvaluator.CanMarkProblem(teamPublic, "sub-owner", false, true, false, new HashSet<int> { 2 }));
            Assert.False(TaskAccessEvaluator.CanMarkProblem(teamPublic, "other", false, false, false, new HashSet<int> { 2 }));

            var globalPublic = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic
            });
            Assert.True(TaskAccessEvaluator.CanMarkProblem(globalPublic, "anyone", false, false, false, new HashSet<int>()));
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
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(notProblem, "any", false, false, false, new HashSet<int>()));

            var privateProblem = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.Private,
                IsProblem = true,
                ProblemReporterId = "reporter"
            });
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(privateProblem, "reporter", false, false, false, new HashSet<int>()));
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(privateProblem, "owner", false, true, false, new HashSet<int>())); // subscription owner who is creator
            Assert.False(TaskAccessEvaluator.CanUnmarkProblem(privateProblem, "stranger", false, false, false, new HashSet<int>()));

            var teamPublicProblem = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                GroupId = 1,
                IsProblem = true,
                ProblemReporterId = "reporter"
            });
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(teamPublicProblem, "reporter", false, false, false, new HashSet<int> { 2 }));
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(teamPublicProblem, "lead", false, false, true, new HashSet<int> { 1 })); // team lead same group
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(teamPublicProblem, "sub-owner", false, true, false, new HashSet<int> { 2 }));
            Assert.False(TaskAccessEvaluator.CanUnmarkProblem(teamPublicProblem, "member2", false, false, false, new HashSet<int> { 2 })); // other group

            var globalProblem = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                VisibilityScope = TaskVisibilityScopes.GlobalPublic,
                IsProblem = true,
                ProblemReporterId = "reporter"
            });
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(globalProblem, "reporter", false, false, false, new HashSet<int>()));
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(globalProblem, "sub-owner", false, true, false, new HashSet<int>()));
            Assert.False(TaskAccessEvaluator.CanUnmarkProblem(globalProblem, "random", false, false, false, new HashSet<int>()));

            var hiddenAssigneeProblem = TaskAccessEvaluator.FromTask(new TaskItem
            {
                CreatedById = "creator",
                AssignedToId = "assignee",
                IsAssigneeVisibleToOthers = false,
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                GroupId = 1,
                IsProblem = true,
                ProblemReporterId = "assignee"
            });
            Assert.True(TaskAccessEvaluator.CanUnmarkProblem(hiddenAssigneeProblem, "assignee", false, false, false, new HashSet<int> { 1 })); // reporter
            Assert.False(TaskAccessEvaluator.CanUnmarkProblem(hiddenAssigneeProblem, "member", false, false, false, new HashSet<int> { 1 })); // hidden assignee blocks others
        }

        [Fact]
        public void TeamPublic_Task_Should_Be_Editable_By_Member_In_Any_Matching_Group()
        {
            var task = new TaskItem
            {
                Id = 10,
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                GroupId = 5
            };
            var snapshot = TaskAccessEvaluator.FromTask(task);

            // user is in groups 2 and 5
            var userGroupIds = new HashSet<int> { 2, 5 };

            var canEdit = TaskAccessEvaluator.CanEditTask(
                snapshot, "other-user",
                isAdmin: false, isSubscriptionOwner: false, isTeamLead: false,
                userGroupIds: userGroupIds
            );

            Assert.True(canEdit);
        }

        [Fact]
        public void TeamPublic_Task_Should_Not_Be_Editable_By_Member_Not_In_Group()
        {
            var task = new TaskItem
            {
                Id = 11,
                CreatedById = "owner",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                GroupId = 5
            };
            var snapshot = TaskAccessEvaluator.FromTask(task);

            var userGroupIds = new HashSet<int> { 2, 3 }; // does not include 5

            var canEdit = TaskAccessEvaluator.CanEditTask(
                snapshot, "other-user",
                isAdmin: false, isSubscriptionOwner: false, isTeamLead: false,
                userGroupIds: userGroupIds
            );

            Assert.False(canEdit);
        }
    }
}
