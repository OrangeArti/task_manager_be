using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace TaskManager.Tests
{
    /// <summary>
    /// RED stubs for KNB-02: PATCH /tasks/{id}/status with new { status: string } shape.
    /// These tests fail until Plan 02 implements the Status enum and new request DTO.
    /// </summary>
    public class KanbanStatusTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public KanbanStatusTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        private HttpRequestMessage AuthedPatch(string url, object body)
        {
            var req = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json")
            };
            req.Headers.Add("X-Test-UserId", "user1");
            req.Headers.Add("X-Test-Role", "User");
            return req;
        }

        [Fact]
        public async Task PatchStatus_WithValidStringEnum_Returns200()
        {
            // Arrange: create a task first
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/tasks")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { title = "Kanban Status Test Task", visibilityScope = "Private" }),
                    Encoding.UTF8, "application/json")
            };
            createReq.Headers.Add("X-Test-UserId", "user1");
            createReq.Headers.Add("X-Test-Role", "User");
            var createResp = await _client.SendAsync(createReq);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
            var taskId = created.GetProperty("id").GetInt32();

            // Act: PATCH with new { status: "InProgress" } shape
            var patchResp = await _client.SendAsync(AuthedPatch($"/api/tasks/{taskId}/status", new { status = "InProgress" }));

            // Assert
            Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);
            var body = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("InProgress", body.GetProperty("status").GetString());
        }

        [Fact]
        public async Task PatchStatus_Done_SetsIsCompletedTrue()
        {
            // Arrange: create a task
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/tasks")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { title = "Done Status Test Task", visibilityScope = "Private" }),
                    Encoding.UTF8, "application/json")
            };
            createReq.Headers.Add("X-Test-UserId", "user1");
            createReq.Headers.Add("X-Test-Role", "User");
            var createResp = await _client.SendAsync(createReq);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
            var taskId = created.GetProperty("id").GetInt32();

            // Act
            var patchResp = await _client.SendAsync(AuthedPatch($"/api/tasks/{taskId}/status", new { status = "Done" }));

            // Assert
            Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);
            var body = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Done", body.GetProperty("status").GetString());
            Assert.True(body.GetProperty("isCompleted").GetBoolean());
        }

        [Fact]
        public async Task PatchStatus_OldIsCompletedShape_Returns400()
        {
            // Arrange: create a task
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/tasks")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { title = "Old Shape Rejection Test", visibilityScope = "Private" }),
                    Encoding.UTF8, "application/json")
            };
            createReq.Headers.Add("X-Test-UserId", "user1");
            createReq.Headers.Add("X-Test-Role", "User");
            var createResp = await _client.SendAsync(createReq);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
            var taskId = created.GetProperty("id").GetInt32();

            // Act: send OLD shape { "isCompleted": true } — must now return 400
            var patchResp = await _client.SendAsync(AuthedPatch($"/api/tasks/{taskId}/status", new { isCompleted = true }));

            // Assert: new DTO requires "status" field; old shape is invalid
            Assert.Equal(HttpStatusCode.BadRequest, patchResp.StatusCode);
        }

        [Fact]
        public async Task PatchStatus_InvalidStatusValue_Returns400()
        {
            // Arrange: create a task
            var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/tasks")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { title = "Invalid Status Test", visibilityScope = "Private" }),
                    Encoding.UTF8, "application/json")
            };
            createReq.Headers.Add("X-Test-UserId", "user1");
            createReq.Headers.Add("X-Test-Role", "User");
            var createResp = await _client.SendAsync(createReq);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
            var taskId = created.GetProperty("id").GetInt32();

            // Act
            var patchResp = await _client.SendAsync(AuthedPatch($"/api/tasks/{taskId}/status", new { status = "Completed" }));

            // Assert: "Completed" is not a valid TaskStatus value
            Assert.Equal(HttpStatusCode.BadRequest, patchResp.StatusCode);
        }

        [Fact]
        public async Task PatchStatus_Unauthenticated_Returns401()
        {
            var req = new HttpRequestMessage(HttpMethod.Patch, "/api/tasks/999/status")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { status = "InProgress" }),
                    Encoding.UTF8, "application/json")
            };
            var response = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
