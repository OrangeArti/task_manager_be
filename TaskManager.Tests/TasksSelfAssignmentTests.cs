using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos;
using TaskManager.Api.Models;
using Xunit;

namespace TaskManager.Tests
{
    public class TasksSelfAssignmentTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public TasksSelfAssignmentTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientForUser(string userId)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
            client.DefaultRequestHeaders.Add("X-Test-Role", "User");
            return client;
        }

        [Fact]
        public async Task AssignSelf_ShouldSuccess_WhenTaskIsTeamPublicAndUnassigned()
        {
            // 1. Create a second user in Team 1 for this test
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (!db.Users.Any(u => u.Id == "user2"))
                {
                    db.Users.Add(new ApplicationUser
                    {
                        Id = "user2",
                        UserName = "user2",
                        Email = "user2@test.local",
                        TeamId = 1,
                        SubscriptionId = "sub-1",
                        KeycloakSubject = "user2"
                    });
                    await db.SaveChangesAsync();
                }
            }

            // 2. Login as User1 and create a Shared Task
            var user1Client = CreateClientForUser("user1");
            var createResponse = await user1Client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
            {
                Title = "Shared Task For Self Assign",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                TeamId = 1
            });
            createResponse.EnsureSuccessStatusCode();
            var task = await createResponse.Content.ReadFromJsonAsync<TaskItemDto>();

            // 3. Login as User2 (Same Team)
            var user2Client = CreateClientForUser("user2");

            // Act: Assign Self
            var assignResponse = await user2Client.PatchAsync($"/api/tasks/{task!.Id}/assign-self", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
            var updatedTask = await assignResponse.Content.ReadFromJsonAsync<TaskItemDto>();
            Assert.Equal("user2", updatedTask!.AssignedToId);
            Assert.True(updatedTask.IsAssigneeVisibleToOthers);
        }

        [Fact]
        public async Task AssignSelf_ShouldFail_WhenTaskAssignedToOthers()
        {
            // 1. Create User3 in Team 1
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (!db.Users.Any(u => u.Id == "user3"))
                {
                    db.Users.Add(new ApplicationUser
                    {
                        Id = "user3",
                        UserName = "user3",
                        Email = "user3@test.local",
                        TeamId = 1,
                        SubscriptionId = "sub-1",
                        KeycloakSubject = "user3"
                    });
                    await db.SaveChangesAsync();
                }
            }

            // Arrange: User1 creates task assigned to User1
            var user1Client = CreateClientForUser("user1");
            var createResponse = await user1Client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
            {
                Title = "Already Taken Task",
                VisibilityScope = TaskVisibilityScopes.TeamPublic,
                TeamId = 1,
                AssignedToId = "user1"
            });
            createResponse.EnsureSuccessStatusCode();
            var task = await createResponse.Content.ReadFromJsonAsync<TaskItemDto>();

            var user3Client = CreateClientForUser("user3");

            // Act
            var assignResponse = await user3Client.PatchAsync($"/api/tasks/{task!.Id}/assign-self", null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, assignResponse.StatusCode);
        }
    }
}
