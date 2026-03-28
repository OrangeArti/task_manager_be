// AuthControllerTests.cs — TOMBSTONED
//
// These tests verified the custom JWT auth endpoints (register, login, refresh, logout).
// The entire AuthController and JWT stack were deleted in Plan 01-03 as part of the
// Keycloak migration (AUTH-02). Auth endpoints now return 404 — see AuthEndpointRemovalTests.
//
// Covered by:
//   - TaskManager.Tests.Auth.AuthEndpointRemovalTests (3 tests — endpoints gone)
//   - TaskManager.Tests.Auth.KeycloakClaimsTransformerTests (5 tests — claims mapping)
