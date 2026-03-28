using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TaskManager.Tests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Если нет ни одного тестового заголовка — считаем, что пользователь НЕ аутентифицирован
        var hasAnyHeader =
            Request.Headers.ContainsKey("X-Test-UserId") ||
            Request.Headers.ContainsKey("X-Test-Role") ||
            Request.Headers.ContainsKey("X-Test-TeamId") ||
            Request.Headers.ContainsKey("X-Test-SubscriptionId");

        if (!hasAnyHeader)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers["X-Test-UserId"].FirstOrDefault() ?? "test-user";
        var role = Request.Headers["X-Test-Role"].FirstOrDefault();
        var teamIdHeader = Request.Headers["X-Test-TeamId"].FirstOrDefault();
        var subscriptionIdHeader = Request.Headers["X-Test-SubscriptionId"].FirstOrDefault();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("sub", userId), // mirrors the Keycloak sub claim used by GetCurrentUserDbIdAsync()
        };

        if (!string.IsNullOrEmpty(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        if (!string.IsNullOrEmpty(teamIdHeader))
            claims.Add(new Claim("team_id", teamIdHeader));

        if (!string.IsNullOrEmpty(subscriptionIdHeader))
            claims.Add(new Claim("subscription_id", subscriptionIdHeader));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}