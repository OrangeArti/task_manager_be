using System.Net;
using System.Net.Http.Json;
using TaskManager.Tests;
using Xunit;

namespace TaskManager.Tests.Auth;

/// <summary>
/// Verifies that the custom auth endpoints are GONE after Keycloak migration.
/// These tests are RED before Plan 03 deletes AuthController, GREEN after.
/// Covers: AUTH-02 (no shadow auth path).
/// </summary>
public class AuthEndpointRemovalTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointRemovalTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostAuthLogin_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "test@example.com", password = "Test123!" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthRegister_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "test@example.com", password = "Test123!", displayName = "Test" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthRefresh_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = "invalid-token" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
