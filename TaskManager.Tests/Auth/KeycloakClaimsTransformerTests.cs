using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using TaskManager.Api.Auth;  // KeycloakClaimsTransformer lives here (Plan 03 creates it)
using Xunit;

namespace TaskManager.Tests.Auth;

public class KeycloakClaimsTransformerTests
{
    private readonly KeycloakClaimsTransformer _sut = new();

    private static ClaimsPrincipal MakePrincipal(string? realmAccessJson = null)
    {
        var claims = new List<Claim>();
        if (realmAccessJson is not null)
            claims.Add(new Claim("realm_access", realmAccessJson));
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task NoRealmAccessClaim_ReturnsUnchangedPrincipal()
    {
        var principal = MakePrincipal();
        var result = await _sut.TransformAsync(principal);
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task SingleRole_AddsClaimTypesRoleClaim()
    {
        var principal = MakePrincipal("{\"roles\":[\"Admin\"]}");
        var result = await _sut.TransformAsync(principal);
        Assert.Single(result.FindAll(ClaimTypes.Role));
        Assert.Equal("Admin", result.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public async Task MultipleRoles_AddsAllRoleClaims()
    {
        var principal = MakePrincipal("{\"roles\":[\"Admin\",\"TeamLead\"]}");
        var result = await _sut.TransformAsync(principal);
        var roles = result.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("Admin", roles);
        Assert.Contains("TeamLead", roles);
        Assert.Equal(2, roles.Count);
    }

    [Fact]
    public async Task CalledTwice_NoDuplicateRoleClaims()
    {
        var principal = MakePrincipal("{\"roles\":[\"Admin\"]}");
        await _sut.TransformAsync(principal);
        var result = await _sut.TransformAsync(principal);
        Assert.Single(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task EmptyRolesArray_NoRoleClaimsAdded()
    {
        var principal = MakePrincipal("{\"roles\":[]}");
        var result = await _sut.TransformAsync(principal);
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }
}
