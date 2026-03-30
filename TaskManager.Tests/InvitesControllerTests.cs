using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManager.Api.Models;
using TaskManager.Api.Authorization;
using Xunit;

namespace TaskManager.Tests;

public class InvitesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public InvitesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AcceptInvite_ValidToken_Returns200AndJoinsOrg()
    {
        // Step 1: Create org as user1
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/orgs");
        createReq.Headers.Add("X-Test-UserId", "user1");
        createReq.Headers.Add("X-Test-Role", "User");
        createReq.Content = JsonContent.Create(new { name = "Invite Accept Org" });
        var createResp = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var org = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetInt32();

        // Step 2: Generate invite as user1 (owner)
        var inviteReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/invites");
        inviteReq.Headers.Add("X-Test-UserId", "user1");
        inviteReq.Headers.Add("X-Test-Role", "User");
        inviteReq.Content = JsonContent.Create(new { email = "user2@test.local" });
        var inviteResp = await _client.SendAsync(inviteReq);
        Assert.Equal(HttpStatusCode.Created, inviteResp.StatusCode);
        var inviteBody = await inviteResp.Content.ReadFromJsonAsync<JsonElement>();
        var inviteUrl = inviteBody.GetProperty("inviteUrl").GetString()!;
        var token = inviteUrl.Replace("/join?token=", "");

        // Step 3: Accept invite as user2
        var acceptReq = new HttpRequestMessage(HttpMethod.Get, $"/api/invites/accept?token={token}");
        acceptReq.Headers.Add("X-Test-UserId", "user2");
        acceptReq.Headers.Add("X-Test-Role", "User");
        var acceptResp = await _client.SendAsync(acceptReq);

        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);
        var acceptBody = await acceptResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orgId, acceptBody.GetProperty("orgId").GetInt32());
    }

    [Fact]
    public async Task AcceptInvite_InvalidToken_Returns400()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/invites/accept?token=00000000000000000000000000000000");
        request.Headers.Add("X-Test-UserId", "user1");
        request.Headers.Add("X-Test-Role", "User");
        var resp = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AcceptInvite_NoToken_Returns400()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/invites/accept");
        request.Headers.Add("X-Test-UserId", "user1");
        request.Headers.Add("X-Test-Role", "User");
        var resp = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AcceptInvite_NoAuth_Returns401()
    {
        var resp = await _client.GetAsync("/api/invites/accept?token=sometoken");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AcceptInvite_AlreadyUsedToken_Returns400()
    {
        // Create org + generate invite
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/orgs");
        createReq.Headers.Add("X-Test-UserId", "user1");
        createReq.Headers.Add("X-Test-Role", "User");
        createReq.Content = JsonContent.Create(new { name = "Used Token Org" });
        var orgResp = await _client.SendAsync(createReq);
        var orgId = (await orgResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var invReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orgs/{orgId}/invites");
        invReq.Headers.Add("X-Test-UserId", "user1");
        invReq.Headers.Add("X-Test-Role", "User");
        invReq.Content = JsonContent.Create(new { email = "lead1@test.local" });
        var invResp = await _client.SendAsync(invReq);
        var token = (await invResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("inviteUrl").GetString()!.Replace("/join?token=", "");

        // First acceptance (user2)
        var accept1 = new HttpRequestMessage(HttpMethod.Get, $"/api/invites/accept?token={token}");
        accept1.Headers.Add("X-Test-UserId", "user2");
        accept1.Headers.Add("X-Test-Role", "User");
        var r1 = await _client.SendAsync(accept1);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // Second acceptance (lead1 tries same token) — must be 400
        var accept2 = new HttpRequestMessage(HttpMethod.Get, $"/api/invites/accept?token={token}");
        accept2.Headers.Add("X-Test-UserId", "lead1");
        accept2.Headers.Add("X-Test-Role", "User");
        var r2 = await _client.SendAsync(accept2);
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
    }
}
