using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManager.Api.Dtos.Orgs;
using Xunit;

namespace TaskManager.Tests;

public class OrgsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrgsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrg_ValidName_Returns201AndOrgDto()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orgs");
        request.Headers.Add("X-Test-UserId", "user1");
        request.Headers.Add("X-Test-Role", "User");
        request.Content = JsonContent.Create(new { name = "Test Corp" });
        var resp = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Test Corp", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task CreateOrg_EmptyName_Returns400()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orgs");
        request.Headers.Add("X-Test-UserId", "user1");
        request.Headers.Add("X-Test-Role", "User");
        request.Content = JsonContent.Create(new { name = "" });
        var resp = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CreateOrg_NoAuth_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/orgs", new { name = "X" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GenerateInvite_NonOwner_Returns403()
    {
        // Create org as user1
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/orgs");
        createReq.Headers.Add("X-Test-UserId", "user1");
        createReq.Headers.Add("X-Test-Role", "User");
        createReq.Content = JsonContent.Create(new { name = "Org For Invite Test" });
        var createResp = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var org = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetInt32();

        // user2 is NOT the owner — should get 403
        var inviteReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/invites");
        inviteReq.Headers.Add("X-Test-UserId", "user2");
        inviteReq.Headers.Add("X-Test-Role", "User");
        inviteReq.Content = JsonContent.Create(new { email = "new@test.local" });
        var inviteResp = await _client.SendAsync(inviteReq);

        Assert.Equal(HttpStatusCode.Forbidden, inviteResp.StatusCode);
    }

    [Fact]
    public async Task GenerateInvite_Owner_Returns201WithInviteUrl()
    {
        // Create org as user1
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/orgs");
        createReq.Headers.Add("X-Test-UserId", "user1");
        createReq.Headers.Add("X-Test-Role", "User");
        createReq.Content = JsonContent.Create(new { name = "Org For Owner Invite" });
        var createResp = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var org = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetInt32();

        // user1 IS the owner
        var inviteReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/invites");
        inviteReq.Headers.Add("X-Test-UserId", "user1");
        inviteReq.Headers.Add("X-Test-Role", "User");
        inviteReq.Content = JsonContent.Create(new { email = "new@test.local" });
        var inviteResp = await _client.SendAsync(inviteReq);

        Assert.Equal(HttpStatusCode.Created, inviteResp.StatusCode);
        var body = await inviteResp.Content.ReadFromJsonAsync<JsonElement>();
        var inviteUrl = body.GetProperty("inviteUrl").GetString();
        Assert.NotNull(inviteUrl);
        Assert.StartsWith("/join?token=", inviteUrl);
        // Token must be 32 chars hex (Guid.NewGuid().ToString("N"))
        var tokenPart = inviteUrl!.Replace("/join?token=", "");
        Assert.Equal(32, tokenPart.Length);
    }
}
