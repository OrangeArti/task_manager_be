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
            // arrange: имитируем авторизованного обычного пользователя
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/tasks");
            request.Headers.Add("X-Test-UserId", "user1");
            request.Headers.Add("X-Test-Role", "User");
            request.Headers.Add("X-Test-TeamId", "1");
            request.Headers.Add("X-Test-SubscriptionId", "sub-1");

            // act
            var response = await _client.SendAsync(request);

            // assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        [Fact]
        public async Task CreateTask_Should_Return_201_For_Authorized_User()
        {
            // arrange: создаём клиент и отправляем заголовки авторизации
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");

            request.Headers.Add("X-Test-UserId", "user1");
            request.Headers.Add("X-Test-Role", "User");
            request.Headers.Add("X-Test-TeamId", "1");
            request.Headers.Add("X-Test-SubscriptionId", "sub-1");

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
            // ---------- arrange: сначала создаём задачу ----------
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");
            createRequest.Headers.Add("X-Test-UserId", "user1");
            createRequest.Headers.Add("X-Test-Role", "User");
            createRequest.Headers.Add("X-Test-TeamId", "1");
            createRequest.Headers.Add("X-Test-SubscriptionId", "sub-1");

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

            // Пытаемся достать Id созданной задачи
            var json = await createResponse.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            int taskId = doc.RootElement.GetProperty("id").GetInt32();

            // ---------- act: отправляем PUT от того же пользователя ----------
            var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/tasks/{taskId}");
            updateRequest.Headers.Add("X-Test-UserId", "user1");
            updateRequest.Headers.Add("X-Test-Role", "User");
            updateRequest.Headers.Add("X-Test-TeamId", "1");
            updateRequest.Headers.Add("X-Test-SubscriptionId", "sub-1");

            var updatePayload = new
            {
                title = "Updated title",
                description = "Updated description",
                visibility = 0 // всё ещё Private
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
            // ---------- arrange: сначала создаём задачу ----------
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");
            createRequest.Headers.Add("X-Test-UserId", "user1");
            createRequest.Headers.Add("X-Test-Role", "User");
            createRequest.Headers.Add("X-Test-TeamId", "1");
            createRequest.Headers.Add("X-Test-SubscriptionId", "sub-1");

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

            // Достаём ID задачи из JSON-ответа (id = int)
            var json = await createResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            int taskId = doc.RootElement.GetProperty("id").GetInt32();

            // ---------- act: удаляем задачу тем же пользователем ----------
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId}");
            deleteRequest.Headers.Add("X-Test-UserId", "user1");
            deleteRequest.Headers.Add("X-Test-Role", "User");
            deleteRequest.Headers.Add("X-Test-TeamId", "1");
            deleteRequest.Headers.Add("X-Test-SubscriptionId", "sub-1");

            var deleteResponse = await _client.SendAsync(deleteRequest);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }
        [Fact]
        public async Task DeleteTask_Should_Return_403_For_NonOwner()
        {
            // ---------- arrange: создаём задачу как user1 ----------
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");
            createRequest.Headers.Add("X-Test-UserId", "user1");
            createRequest.Headers.Add("X-Test-Role", "User");
            createRequest.Headers.Add("X-Test-TeamId", "1");
            createRequest.Headers.Add("X-Test-SubscriptionId", "sub-1");

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

            // ---------- act: пробуем удалить задачу как другой пользователь (user2) ----------
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId}");
            deleteRequest.Headers.Add("X-Test-UserId", "user2");           // ⬅️ другой пользователь
            deleteRequest.Headers.Add("X-Test-Role", "User");
            deleteRequest.Headers.Add("X-Test-TeamId", "1");
            deleteRequest.Headers.Add("X-Test-SubscriptionId", "sub-1");

            var deleteResponse = await _client.SendAsync(deleteRequest);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
        }
        [Fact]
        public async Task DeleteTask_Should_Return_404_For_NonExisting_Task()
        {
            // ---------- arrange: авторизованный пользователь ----------
            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/tasks/999999");
            request.Headers.Add("X-Test-UserId", "user1");
            request.Headers.Add("X-Test-Role", "User");
            request.Headers.Add("X-Test-TeamId", "1");
            request.Headers.Add("X-Test-SubscriptionId", "sub-1");

            // ---------- act ----------
            var response = await _client.SendAsync(request);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        [Fact]
        public async Task Delete_GlobalPublic_Task_Should_Be_Allowed_For_SubscriptionOwner()
        {
            // ---------- arrange: создаём GlobalPublic задачу как обычный пользователь ----------
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");
            createRequest.Headers.Add("X-Test-UserId", "user1");
            createRequest.Headers.Add("X-Test-Role", "User");
            createRequest.Headers.Add("X-Test-TeamId", "1");
            createRequest.Headers.Add("X-Test-SubscriptionId", "sub-1");

            var createPayload = new
            {
                title = "Global task",
                description = "Created by regular user",
                visibilityScope = "GlobalPublic" // GlobalPublic (0 = Private, 1 = TeamPublic, 2 = GlobalPublic)
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

            // ---------- act: удаляем эту задачу как SubscriptionOwner ----------
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId}");
            deleteRequest.Headers.Add("X-Test-UserId", "sub-owner");
            deleteRequest.Headers.Add("X-Test-Role", "SubscriptionOwner");
            deleteRequest.Headers.Add("X-Test-TeamId", "1");          // команда не так важна, но пусть будет
            deleteRequest.Headers.Add("X-Test-SubscriptionId", "sub-1"); // тот же subscription

            var deleteResponse = await _client.SendAsync(deleteRequest);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }
        [Fact]
        public async Task Delete_TeamPublic_Task_Should_Be_Allowed_For_TeamLead_Of_Same_Team()
        {
            // ---------- arrange: создаём TeamPublic задачу как обычный пользователь в команде 1 ----------
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tasks");
            createRequest.Headers.Add("X-Test-UserId", "user1");
            createRequest.Headers.Add("X-Test-Role", "User");
            createRequest.Headers.Add("X-Test-TeamId", "1");
            createRequest.Headers.Add("X-Test-SubscriptionId", "sub-1");

            var createPayload = new
            {
                title = "TeamPublic task",
                description = "Created by regular user in team 1",
                visibilityScope = "TeamPublic", // ⬅️ важно: строка, как в CreateTaskRequest
                teamId = 1
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

            // ---------- act: удаляем эту задачу как TeamLead той же команды (team 1) ----------
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId}");
            deleteRequest.Headers.Add("X-Test-UserId", "lead1");
            deleteRequest.Headers.Add("X-Test-Role", "TeamLead");
            deleteRequest.Headers.Add("X-Test-TeamId", "1");          // ⬅️ та же команда
            deleteRequest.Headers.Add("X-Test-SubscriptionId", "sub-1");

            var deleteResponse = await _client.SendAsync(deleteRequest);

            // ---------- assert ----------
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }
    }
}
