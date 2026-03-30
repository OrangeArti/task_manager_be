using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaskManager.Tests;

public class GroupsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GroupsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Helper: create an org as user1 and return its ID
    private async Task<int> CreateOrgAsync(string orgName = "Test Org For Groups")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/orgs");
        req.Headers.Add("X-Test-UserId", "user1");
        req.Headers.Add("X-Test-Role", "User");
        req.Content = JsonContent.Create(new { name = orgName });
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    // Helper: create a group in an org as user1 (who is owner) and return its ID
    private async Task<int> CreateGroupAsync(int orgId, string groupName = "Test Group")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/groups");
        req.Headers.Add("X-Test-UserId", "user1");
        req.Headers.Add("X-Test-Role", "User");
        req.Content = JsonContent.Create(new { name = groupName });
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task CreateGroup_Owner_Returns201()
    {
        var orgId = await CreateOrgAsync("Org For Create Group");
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/groups");
        req.Headers.Add("X-Test-UserId", "user1");
        req.Headers.Add("X-Test-Role", "User");
        req.Content = JsonContent.Create(new { name = "My Group" });
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("My Group", body.GetProperty("name").GetString());
        Assert.Equal(orgId, body.GetProperty("organizationId").GetInt32());
    }

    [Fact]
    public async Task CreateGroup_EmptyName_Returns400()
    {
        var orgId = await CreateOrgAsync("Org For Empty Name");
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/groups");
        req.Headers.Add("X-Test-UserId", "user1");
        req.Headers.Add("X-Test-Role", "User");
        req.Content = JsonContent.Create(new { name = "" });
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CreateGroup_NonOwner_Returns403()
    {
        var orgId = await CreateOrgAsync("Org For NonOwner Group");

        // user2 is not the owner
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/groups");
        req.Headers.Add("X-Test-UserId", "user2");
        req.Headers.Add("X-Test-Role", "User");
        req.Content = JsonContent.Create(new { name = "Unauthorized Group" });
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GetGroups_ReturnsListForOrg()
    {
        var orgId = await CreateOrgAsync("Org For List Groups");
        await CreateGroupAsync(orgId, "Alpha");
        await CreateGroupAsync(orgId, "Beta");

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/orgs/{orgId}/groups");
        req.Headers.Add("X-Test-UserId", "user1");
        req.Headers.Add("X-Test-Role", "User");
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.True(body.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task UpdateGroup_Owner_Returns200WithNewName()
    {
        var orgId = await CreateOrgAsync("Org For Update Group");
        var groupId = await CreateGroupAsync(orgId, "Old Name");

        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/orgs/{orgId}/groups/{groupId}");
        req.Headers.Add("X-Test-UserId", "user1");
        req.Headers.Add("X-Test-Role", "User");
        req.Content = JsonContent.Create(new { name = "New Name" });
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("New Name", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task DeleteGroup_Owner_Returns204()
    {
        var orgId = await CreateOrgAsync("Org For Delete Group");
        var groupId = await CreateGroupAsync(orgId, "To Delete");

        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/orgs/{orgId}/groups/{groupId}");
        req.Headers.Add("X-Test-UserId", "user1");
        req.Headers.Add("X-Test-Role", "User");
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteGroup_NonOwner_Returns403()
    {
        var orgId = await CreateOrgAsync("Org For Delete Forbidden");
        var groupId = await CreateGroupAsync(orgId, "Protected Group");

        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/orgs/{orgId}/groups/{groupId}");
        req.Headers.Add("X-Test-UserId", "user2");
        req.Headers.Add("X-Test-Role", "User");
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AddMember_Owner_Returns201()
    {
        var orgId = await CreateOrgAsync("Org For Add Member");
        var groupId = await CreateGroupAsync(orgId, "Members Group");

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/groups/{groupId}/members");
        req.Headers.Add("X-Test-UserId", "user1");
        req.Headers.Add("X-Test-Role", "User");
        req.Content = JsonContent.Create(new { userId = "user2" });
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task AddMember_NonOwner_Returns403()
    {
        var orgId = await CreateOrgAsync("Org For AddMember Forbidden");
        var groupId = await CreateGroupAsync(orgId, "Protected Members");

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/groups/{groupId}/members");
        req.Headers.Add("X-Test-UserId", "user2"); // not owner
        req.Headers.Add("X-Test-Role", "User");
        req.Content = JsonContent.Create(new { userId = "lead1" });
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_Owner_Returns204()
    {
        var orgId = await CreateOrgAsync("Org For Remove Member");
        var groupId = await CreateGroupAsync(orgId, "Group To Leave");

        // Add member first
        var addReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/groups/{groupId}/members");
        addReq.Headers.Add("X-Test-UserId", "user1");
        addReq.Headers.Add("X-Test-Role", "User");
        addReq.Content = JsonContent.Create(new { userId = "user2" });
        await _client.SendAsync(addReq);

        // Remove member
        var removeReq = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/orgs/{orgId}/groups/{groupId}/members/user2");
        removeReq.Headers.Add("X-Test-UserId", "user1");
        removeReq.Headers.Add("X-Test-Role", "User");
        var resp = await _client.SendAsync(removeReq);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task NoAuth_Returns401()
    {
        var resp = await _client.GetAsync("/api/orgs/1/groups");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
