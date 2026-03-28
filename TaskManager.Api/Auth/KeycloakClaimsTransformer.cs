using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace TaskManager.Api.Auth;

/// <summary>
/// Stub: transforms Keycloak realm_access JWT claims into standard ClaimTypes.Role claims.
/// Real implementation provided in Plan 03.
/// </summary>
public class KeycloakClaimsTransformer : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        throw new NotImplementedException("KeycloakClaimsTransformer not yet implemented — see Plan 03.");
    }
}
