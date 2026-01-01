using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos;
using TaskManager.Api.Models;
using Xunit;

namespace TaskManager.Tests
{
    public class TasksControllerAdvancedTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public TasksControllerAdvancedTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Filter_By_IsCompleted_Should_Return_Only_Matching()
        {
            await ResetTasksAsync(db =>
            {
                db.Tasks.AddRange(
                    new TaskItem
                    {
                        Title = "Done",
                        CreatedById = "user1",
                        VisibilityScope = TaskVisibilityScopes.Private,
                        IsCompleted = true
                    },
                    new TaskItem
                    {
                        Title = "Pending",
                        CreatedById = "user1",
                        VisibilityScope = TaskVisibilityScopes.Private,
                        IsCompleted = false
                    });
            });

            var completed = await SendGetAsync("/api/tasks?isCompleted=true");
            Assert.All(completed.Items, t => Assert.True(t.IsCompleted));
            Assert.Single(completed.Items);

            var pending = await SendGetAsync("/api/tasks?isCompleted=false");
            Assert.All(pending.Items, t => Assert.False(t.IsCompleted));
            Assert.Single(pending.Items);
        }

        [Fact]
        public async Task Filter_By_Priority_Should_Return_Only_Matching()
        {
            await ResetTasksAsync(db =>
            {
                db.Tasks.AddRange(
                    new TaskItem { Title = "Low", CreatedById = "user1", Priority = 0, VisibilityScope = TaskVisibilityScopes.Private },
                    new TaskItem { Title = "Medium", CreatedById = "user1", Priority = 1, VisibilityScope = TaskVisibilityScopes.Private },
                    new TaskItem { Title = "High", CreatedById = "user1", Priority = 2, VisibilityScope = TaskVisibilityScopes.Private }
                );
            });

            var high = await SendGetAsync("/api/tasks?priority=2");
            Assert.Single(high.Items);
            Assert.Equal("High", high.Items[0].Title);
        }

        [Fact]
        public async Task Search_Should_Match_Title_And_Description()
        {
            await ResetTasksAsync(db =>
            {
                db.Tasks.AddRange(
                    new TaskItem { Title = "Alpha report", Description = "Something", CreatedById = "user1", VisibilityScope = TaskVisibilityScopes.Private },
                    new TaskItem { Title = "Beta item", Description = "alpha keyword inside", CreatedById = "user1", VisibilityScope = TaskVisibilityScopes.Private },
                    new TaskItem { Title = "Gamma", Description = "Other", CreatedById = "user1", VisibilityScope = TaskVisibilityScopes.Private }
                );
            });

            var result = await SendGetAsync("/api/tasks?search=alpha");
            Assert.Equal(2, result.Items.Count);
            Assert.DoesNotContain(result.Items, t => t.Title == "Gamma");
        }

        [Fact]
        public async Task Sorting_Should_Order_By_Selected_Field()
        {
            await ResetTasksAsync(db =>
            {
                db.Tasks.AddRange(
                    new TaskItem
                    {
                        Title = "C-title",
                        CreatedById = "user1",
                        VisibilityScope = TaskVisibilityScopes.Private,
                        CreatedAt = new DateTime(2025, 1, 1),
                        DueDate = new DateTime(2025, 1, 10),
                        Priority = 2
                    },
                    new TaskItem
                    {
                        Title = "A-title",
                        CreatedById = "user1",
                        VisibilityScope = TaskVisibilityScopes.Private,
                        CreatedAt = new DateTime(2024, 12, 1),
                        DueDate = new DateTime(2025, 1, 5),
                        Priority = 1
                    },
                    new TaskItem
                    {
                        Title = "B-title",
                        CreatedById = "user1",
                        VisibilityScope = TaskVisibilityScopes.Private,
                        CreatedAt = new DateTime(2025, 2, 1),
                        DueDate = new DateTime(2025, 1, 15),
                        Priority = 0
                    }
                );
            });

            var byCreatedAsc = await SendGetAsync("/api/tasks?sortBy=createdAt&sortDir=asc");
            Assert.Equal(new[] { "A-title", "C-title", "B-title" }, byCreatedAsc.Items.Select(t => t.Title));

            var byDueDesc = await SendGetAsync("/api/tasks?sortBy=dueDate&sortDir=desc");
            Assert.Equal(new[] { "B-title", "C-title", "A-title" }, byDueDesc.Items.Select(t => t.Title));

            var byPriorityDesc = await SendGetAsync("/api/tasks?sortBy=priority&sortDir=desc");
            Assert.Equal(new[] { "C-title", "A-title", "B-title" }, byPriorityDesc.Items.Select(t => t.Title));

            var byTitleAsc = await SendGetAsync("/api/tasks?sortBy=title&sortDir=asc");
            Assert.Equal(new[] { "A-title", "B-title", "C-title" }, byTitleAsc.Items.Select(t => t.Title));
        }

        [Fact]
        public async Task Visibility_Should_Respect_Scopes_And_Assignee_Privacy()
        {
            await ResetTasksAsync(async db =>
            {
                await EnsureUserAsync("user1", "user1@test.local", teamId: 1);
                var user2 = await EnsureUserAsync("user2", "user2@test.local", teamId: 2);
                if (!await db.Teams.AnyAsync(t => t.Id == 2))
                {
                    db.Teams.Add(new Team { Id = 2, Name = "Team 2" });
                }

                db.Tasks.AddRange(
                    // visible: own private
                    new TaskItem { Title = "own-private", CreatedById = "user1", VisibilityScope = TaskVisibilityScopes.Private },
                    // visible: assigned to user1 even if hidden
                    new TaskItem
                    {
                        Title = "assigned-hidden",
                        CreatedById = "lead1",
                        VisibilityScope = TaskVisibilityScopes.TeamPublic,
                        TeamId = 1,
                        AssignedToId = "user1",
                        IsAssigneeVisibleToOthers = false
                    },
                    // visible: team public same team
                    new TaskItem
                    {
                        Title = "team-public",
                        CreatedById = "lead1",
                        VisibilityScope = TaskVisibilityScopes.TeamPublic,
                        TeamId = 1,
                        IsAssigneeVisibleToOthers = true
                    },
                    // visible: global public
                    new TaskItem
                    {
                        Title = "global-public",
                        CreatedById = "user2",
                        VisibilityScope = TaskVisibilityScopes.GlobalPublic,
                        IsAssigneeVisibleToOthers = true
                    },
                    // not visible: other private
                    new TaskItem { Title = "other-private", CreatedById = "user2", VisibilityScope = TaskVisibilityScopes.Private },
                    // not visible: team public other team
                    new TaskItem
                    {
                        Title = "other-team",
                        CreatedById = "user2",
                        VisibilityScope = TaskVisibilityScopes.TeamPublic,
                        TeamId = 2,
                        IsAssigneeVisibleToOthers = true
                    },
                    // not visible: hidden assignee, not creator/assignee
                    new TaskItem
                    {
                        Title = "hidden-assignee",
                        CreatedById = "user2",
                        VisibilityScope = TaskVisibilityScopes.TeamPublic,
                        TeamId = 1,
                        AssignedToId = "user2",
                        IsAssigneeVisibleToOthers = false
                    }
                );
            });

            var result = await SendGetAsync("/api/tasks");
            var titles = result.Items.Select(t => t.Title).ToList();

            Assert.Contains("own-private", titles);
            Assert.Contains("assigned-hidden", titles);
            Assert.Contains("team-public", titles);
            Assert.Contains("global-public", titles);

            Assert.DoesNotContain("other-private", titles);
            Assert.DoesNotContain("other-team", titles);
            Assert.DoesNotContain("hidden-assignee", titles);
        }

        [Fact]
        public async Task Mark_Problem_Should_Respect_Permissions_And_Idempotency()
        {
            int taskId = 0;
            await ResetTasksAsync(async db =>
            {
                var task = new TaskItem
                {
                    Title = "problem-task",
                    CreatedById = "owner",
                    VisibilityScope = TaskVisibilityScopes.Private
                };
                db.Tasks.Add(task);
                await EnsureUserAsync("owner", "owner@test.local", teamId: 1);
                await EnsureUserAsync("user2", "user2@test.local", teamId: 2);
                await db.SaveChangesAsync();
                taskId = task.Id;
            });

            // user without rights
            var forbiddenRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/tasks/{taskId}/problem")
            {
                Content = JsonContent(new MarkProblemRequest { Description = "not allowed" })
            };
            AddAuth(forbiddenRequest, "user2", "User", teamId: "2");
            var forbiddenResponse = await _client.SendAsync(forbiddenRequest);
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

            // owner marks problem
            var markRequest = CreateProblemPatch(taskId, "issue", "owner");
            var markResponse = await _client.SendAsync(markRequest);
            Assert.Equal(HttpStatusCode.OK, markResponse.StatusCode);

            // idempotent second call with same description
            var markAgain = await _client.SendAsync(CreateProblemPatch(taskId, "issue", "owner"));
            Assert.Equal(HttpStatusCode.NoContent, markAgain.StatusCode);
        }

        [Fact]
        public async Task Unmark_Problem_Should_Respect_Permissions_And_Idempotency()
        {
            int taskId = 0;
            await ResetTasksAsync(async db =>
            {
                await EnsureUserAsync("owner", "owner@test.local", teamId: 1);
                await EnsureUserAsync("user2", "user2@test.local", teamId: 2);
                var task = new TaskItem
                {
                    Title = "problem-task",
                    CreatedById = "owner",
                    VisibilityScope = TaskVisibilityScopes.Private,
                    IsProblem = true,
                    ProblemDescription = "issue",
                    ProblemReporterId = "owner",
                    ProblemReportedAt = DateTime.UtcNow
                };
                db.Tasks.Add(task);
                await db.SaveChangesAsync();
                taskId = task.Id;
            });

            // user without rights
            var forbiddenRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId}/problem");
            AddAuth(forbiddenRequest, "user2", "User", teamId: "2");
            var forbiddenResponse = await _client.SendAsync(forbiddenRequest);
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

            // owner unmarks
            var unmarkRequest = CreateProblemDelete(taskId, "owner");
            var unmarkResponse = await _client.SendAsync(unmarkRequest);
            Assert.Equal(HttpStatusCode.OK, unmarkResponse.StatusCode);

            // idempotent second call when already clear
            var unmarkAgain = await _client.SendAsync(CreateProblemDelete(taskId, "owner"));
            Assert.Equal(HttpStatusCode.NoContent, unmarkAgain.StatusCode);
        }

        private async Task ResetTasksAsync(Action<ApplicationDbContext> seed)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.Tasks.RemoveRange(db.Tasks);
            await db.SaveChangesAsync();

            seed(db);
            await db.SaveChangesAsync();
        }

        private async Task ResetTasksAsync(Func<ApplicationDbContext, Task> seedAsync)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.Tasks.RemoveRange(db.Tasks);
            await db.SaveChangesAsync();

            await seedAsync(db);
            await db.SaveChangesAsync();
        }

        private async Task<ApplicationUser> EnsureUserAsync(string id, string email, int? teamId = null)
        {
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByIdAsync(id);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = id,
                    UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    TeamId = teamId,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };
                await userManager.CreateAsync(user, "Str0ngP@ss!");
            }
            else if (teamId.HasValue && user.TeamId != teamId)
            {
                user.TeamId = teamId;
                await userManager.UpdateAsync(user);
            }

            return user;
        }

        private async Task<PagedResult<TaskItemDto>> SendGetAsync(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            AddAuth(request, "user1", "User", teamId: "1");

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<PagedResult<TaskItemDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(result);
            return result!;
        }

        private static void AddAuth(HttpRequestMessage request, string userId, string role, string teamId, string subscriptionId = "sub-1")
        {
            request.Headers.Add("X-Test-UserId", userId);
            request.Headers.Add("X-Test-Role", role);
            request.Headers.Add("X-Test-TeamId", teamId);
            request.Headers.Add("X-Test-SubscriptionId", subscriptionId);
        }

        private static StringContent JsonContent<T>(T value) =>
            new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

        private HttpRequestMessage CreateProblemPatch(int taskId, string description, string userId)
        {
            var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/tasks/{taskId}/problem")
            {
                Content = JsonContent(new MarkProblemRequest { Description = description })
            };
            AddAuth(req, userId, "User", teamId: "1");
            return req;
        }

        private HttpRequestMessage CreateProblemDelete(int taskId, string userId)
        {
            var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId}/problem");
            AddAuth(req, userId, "User", teamId: "1");
            return req;
        }
    }
}
