using System.Net;
using System.Threading.Tasks;
using Xunit;
using System.Text.Json;

namespace TaskManager.Tests
{
    public class TasksApiTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public TasksApiTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetTasks_Should_Return_401_When_Unauthorized()
        {
            // act
            var response = await _client.GetAsync("/api/tasks");

            // assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        [Fact]
        public async Task GetTasks_Should_Return_200_For_Authorized_User()
        {
            // arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
            request.Headers.Add("X-Test-UserId", "user1");
            request.Headers.Add("X-Test-Role", "User");

            // act
            var response = await _client.SendAsync(request);

            // assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        [Fact]
        public async Task CreateTask_Should_Return_201_For_Authorized_User()
        {
            // arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");

            request.Headers.Add("X-Test-UserId", "user1");
            request.Headers.Add("X-Test-Role", "User");

            var payload = new
            {
                title = "Test Private Task",
                description = "Private test description",
                visibility = 0, // Private
                assignedToId = (string?)null
            };

            request.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            // act
            var response = await _client.SendAsync(request);

            // assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        [Fact]
        public async Task UpdateTask_Should_Return_200_For_Owner()
        {
            // ---------- arrange: create a task first ----------
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");
            createRequest.Headers.Add("X-Test-UserId", "user1");
            createRequest.Headers.Add("X-Test-Role", "User");

            var createPayload = new
            {
                title = "Original title",
                description = "Original description",
                visibility = 0 // Private
            };

            createRequest.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(createPayload),
                System.Text.Encoding.UTF8,
                "application/json");

            var createResponse = await _client.SendAsync(createRequest);
            createResponse.EnsureSuccessStatusCode();

            var json = await createResponse.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            int taskId = doc.RootElement.GetProperty("id").GetInt32();

            // ---------- act: update the task as the same user ----------
            var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/tasks/{taskId}");
            updateRequest.Headers.Add("X-Test-UserId", "user1");
            updateRequest.Headers.Add("X-Test-Role", "User");

            var updatePayload = new
            {
                title = "Updated title",
                description = "Updated description",
                visibility = 0 // still Private
            };

            updateRequest.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(updatePayload),
                System.Text.Encoding.UTF8,
                "application/json");

            var updateResponse = await _client.SendAsync(updateRequest);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        }
        [Fact]
        public async Task DeleteTask_Should_Return_200_For_Owner()
        {
            // ---------- arrange: create a task first ----------
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");
            createRequest.Headers.Add("X-Test-UserId", "user1");
            createRequest.Headers.Add("X-Test-Role", "User");

            var createPayload = new
            {
                title = "Task To Delete",
                description = "Delete me",
                visibility = 0 // Private
            };

            createRequest.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(createPayload),
                System.Text.Encoding.UTF8,
                "application/json");

            var createResponse = await _client.SendAsync(createRequest);
            createResponse.EnsureSuccessStatusCode();

            var json = await createResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            int taskId = doc.RootElement.GetProperty("id").GetInt32();

            // ---------- act: delete as the task owner ----------
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId}");
            deleteRequest.Headers.Add("X-Test-UserId", "user1");
            deleteRequest.Headers.Add("X-Test-Role", "User");

            var deleteResponse = await _client.SendAsync(deleteRequest);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }
        [Fact]
        public async Task DeleteTask_Should_Return_403_For_NonOwner()
        {
            // ---------- arrange: create a task as user1 ----------
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");
            createRequest.Headers.Add("X-Test-UserId", "user1");
            createRequest.Headers.Add("X-Test-Role", "User");

            var createPayload = new
            {
                title = "Task Not Owned",
                description = "Created by user1",
                visibility = 0 // Private
            };

            createRequest.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(createPayload),
                System.Text.Encoding.UTF8,
                "application/json");

            var createResponse = await _client.SendAsync(createRequest);
            createResponse.EnsureSuccessStatusCode();

            var json = await createResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            int taskId = doc.RootElement.GetProperty("id").GetInt32();

            // ---------- act: attempt delete as a different user (user2) ----------
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId}");
            deleteRequest.Headers.Add("X-Test-UserId", "user2");
            deleteRequest.Headers.Add("X-Test-Role", "User");

            var deleteResponse = await _client.SendAsync(deleteRequest);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
        }
        [Fact]
        public async Task DeleteTask_Should_Return_404_For_NonExisting_Task()
        {
            // ---------- arrange: authorized user ----------
            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/tasks/999999");
            request.Headers.Add("X-Test-UserId", "user1");
            request.Headers.Add("X-Test-Role", "User");

            // ---------- act ----------
            var response = await _client.SendAsync(request);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        [Fact]
        public async Task Delete_GlobalPublic_Task_Should_Be_Allowed_For_SubscriptionOwner()
        {
            // ---------- arrange: create a GlobalPublic task as regular user ----------
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");
            createRequest.Headers.Add("X-Test-UserId", "user1");
            createRequest.Headers.Add("X-Test-Role", "User");

            var createPayload = new
            {
                title = "Global task",
                description = "Created by regular user",
                visibilityScope = "GlobalPublic"
            };

            createRequest.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(createPayload),
                System.Text.Encoding.UTF8,
                "application/json");

            var createResponse = await _client.SendAsync(createRequest);
            createResponse.EnsureSuccessStatusCode();

            var json = await createResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            int taskId = doc.RootElement.GetProperty("id").GetInt32();

            // ---------- act: delete as SubscriptionOwner ----------
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId}");
            deleteRequest.Headers.Add("X-Test-UserId", "sub-owner");
            deleteRequest.Headers.Add("X-Test-Role", "SubscriptionOwner");

            var deleteResponse = await _client.SendAsync(deleteRequest);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }
        [Fact]
        public async Task Delete_TeamPublic_Task_Should_Be_Allowed_For_TeamLead_Of_Same_Team()
        {
            // ---------- arrange: create a TeamPublic task in group 1 ----------
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");
            createRequest.Headers.Add("X-Test-UserId", "user1");
            createRequest.Headers.Add("X-Test-Role", "User");

            var createPayload = new
            {
                title = "TeamPublic task",
                description = "Created by regular user in group 1",
                visibilityScope = "TeamPublic",
                groupId = 1
            };

            createRequest.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(createPayload),
                System.Text.Encoding.UTF8,
                "application/json");

            var createResponse = await _client.SendAsync(createRequest);
            createResponse.EnsureSuccessStatusCode();

            var json = await createResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            int taskId = doc.RootElement.GetProperty("id").GetInt32();

            // ---------- act: delete as TeamLead (lead1, who is in group 1) ----------
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId}");
            deleteRequest.Headers.Add("X-Test-UserId", "lead1");
            deleteRequest.Headers.Add("X-Test-Role", "TeamLead");

            var deleteResponse = await _client.SendAsync(deleteRequest);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }
    }
}
