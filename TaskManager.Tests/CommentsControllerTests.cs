using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using TaskManager.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace TaskManager.Tests;

public class CommentsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public CommentsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // Seed a task directly in the DB and return its ID.
    // Factory does NOT pre-seed tasks — each test seeds its own.
    private async Task<int> SeedTaskAsync(
        string createdById,
        string visibilityScope,
        int? groupId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskManager.Api.Data.ApplicationDbContext>();
        var task = new TaskItem
        {
            Title = $"Test Task {Guid.NewGuid()}",
            Description = "Test",
            CreatedById = createdById,
            VisibilityScope = visibilityScope,
            GroupId = groupId,
            IsAssigneeVisibleToOthers = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }

    // Seed a comment directly and return its ID.
    private async Task<int> SeedCommentAsync(int taskId, string authorId, string content = "Test comment", DateTime? createdAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskManager.Api.Data.ApplicationDbContext>();
        var comment = new Comment
        {
            TaskId = taskId,
            AuthorId = authorId,
            Content = content,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        return comment.Id;
    }

    private static HttpRequestMessage AuthAs(HttpMethod method, string url, string userId, string role = "User")
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-UserId", userId);
        req.Headers.Add("X-Test-Role", role);
        return req;
    }

    // ─── CMT-01: POST visibility ────────────────────────────────────────────

    [Fact]
    public async Task PostComment_GlobalPublicTask_AnyUser_Returns201()
    {
        var taskId = await SeedTaskAsync("user1", "GlobalPublic");
        var req = AuthAs(HttpMethod.Post, $"/api/tasks/{taskId}/comments", "user2");
        req.Content = JsonContent.Create(new { content = "Hello from user2" });

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task PostComment_TeamPublicTask_GroupMember_Returns201()
    {
        // user1 is in Group 1 (seeded by factory)
        var taskId = await SeedTaskAsync("user1", "TeamPublic", groupId: 1);
        var req = AuthAs(HttpMethod.Post, $"/api/tasks/{taskId}/comments", "user2"); // user2 also in Group 1
        req.Content = JsonContent.Create(new { content = "Group member comment" });

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task PostComment_PrivateTask_Creator_Returns201()
    {
        var taskId = await SeedTaskAsync("user1", "Private");
        var req = AuthAs(HttpMethod.Post, $"/api/tasks/{taskId}/comments", "user1");
        req.Content = JsonContent.Create(new { content = "My private note" });

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task PostComment_PrivateTask_OtherUser_Returns404()
    {
        // user2 cannot see user1's Private task
        var taskId = await SeedTaskAsync("user1", "Private");
        var req = AuthAs(HttpMethod.Post, $"/api/tasks/{taskId}/comments", "user2");
        req.Content = JsonContent.Create(new { content = "Should not work" });

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetComments_TaskNotVisibleToUser_Returns404()
    {
        // user2 cannot see user1's Private task
        var taskId = await SeedTaskAsync("user1", "Private");
        var req = AuthAs(HttpMethod.Get, $"/api/tasks/{taskId}/comments", "user2");

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ─── CMT-02: DELETE authorization ───────────────────────────────────────

    [Fact]
    public async Task DeleteComment_OwnComment_Returns204()
    {
        var taskId = await SeedTaskAsync("user1", "GlobalPublic");
        var commentId = await SeedCommentAsync(taskId, "user1");

        var req = AuthAs(HttpMethod.Delete, $"/api/tasks/{taskId}/comments/{commentId}", "user1");
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_OtherUserComment_Returns403()
    {
        var taskId = await SeedTaskAsync("user1", "GlobalPublic");
        var commentId = await SeedCommentAsync(taskId, "user1");

        var req = AuthAs(HttpMethod.Delete, $"/api/tasks/{taskId}/comments/{commentId}", "user2");
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_AdminDeletesAnyComment_Returns204()
    {
        var taskId = await SeedTaskAsync("user1", "GlobalPublic");
        var commentId = await SeedCommentAsync(taskId, "user1");

        var req = AuthAs(HttpMethod.Delete, $"/api/tasks/{taskId}/comments/{commentId}", "lead1", role: "Admin");
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ─── CMT-03: GET list ordering ───────────────────────────────────────────

    [Fact]
    public async Task GetComments_ReturnsListOrderedByCreatedAtAsc()
    {
        var taskId = await SeedTaskAsync("user1", "GlobalPublic");
        var baseTime = DateTime.UtcNow.AddMinutes(-5);
        await SeedCommentAsync(taskId, "user1", "First comment", baseTime);
        await SeedCommentAsync(taskId, "user2", "Second comment", baseTime.AddMinutes(2));

        var req = AuthAs(HttpMethod.Get, $"/api/tasks/{taskId}/comments", "user1");
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        var t0 = items[0].GetProperty("createdAt").GetDateTime();
        var t1 = items[1].GetProperty("createdAt").GetDateTime();
        Assert.True(t0 <= t1, "Comments must be ordered oldest-first (CreatedAt ASC)");
    }

    [Fact]
    public async Task GetComments_NoComments_ReturnsEmptyArray()
    {
        var taskId = await SeedTaskAsync("user1", "GlobalPublic");
        var req = AuthAs(HttpMethod.Get, $"/api/tasks/{taskId}/comments", "user1");
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.EnumerateArray().ToList();
        Assert.Empty(items);
    }
}
