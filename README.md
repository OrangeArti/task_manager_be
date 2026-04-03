# Task Manager Backend

RESTful backend for multi-tenant task management. Built with ASP.NET Core 9 and Entity Framework Core. Authentication via Keycloak 26 (replaces custom JWT). Includes organization/group management, policy-based authorization, and extensive integration test coverage.

## What it does
- Manages tasks with visibility scopes (Private, TeamPublic, GlobalPublic), group-scoped access, self-assignment, priority, due dates, completion tracking, and problem flags.
- Supports multi-tenant organizations: org creation, invite-based membership, and subscription ownership.
- Group CRUD with multi-group membership — task visibility evaluated against the user's full set of group memberships.
- User directory with role-scoped field visibility (admin/owner see management fields; regular users see public fields only).
- Role assignment (Admin, TeamLead) managed via `RolesController`.
- Health checks with database connectivity, pending migrations metadata, and trace IDs; structured logging with scopes and correlation headers.

## Getting Started

### Prerequisites
- .NET 9 SDK
- SQL Server (local dev) — or use the in-memory DB for tests (no SQL Server needed)
- Keycloak 26 (provided via Docker Compose for local dev)

### Clone and restore
```bash
git clone <repo-url>
cd task-manager-backend
dotnet restore
```

### Configuration
Set the following in `appsettings.json` or as environment variables:
- `ConnectionStrings:DefaultConnection` — SQL Server connection string
- `Keycloak:auth-server-url` — Keycloak base URL (e.g. `http://localhost:8180`)
- `Keycloak:realm` — Keycloak realm name
- `Keycloak:resource` — Keycloak client ID
- `AllowedCorsOrigins` — origins for the frontend

### Migrate and run (local SQL Server)
```bash
dotnet ef database update --project TaskManager.Api
dotnet run --project TaskManager.Api
```
App starts at http://localhost:5000 (or https://localhost:5001).

### Run with in-memory DB (tests/dev)
Set `ASPNETCORE_ENVIRONMENT=Testing` — no migrations needed:
```bash
dotnet run --project TaskManager.Api
```

### Docker Compose (full stack)
Starts SQL Server, Tasks API, API Gateway, and Keycloak:
```bash
export MSSQL_SA_PASSWORD="YourStrong!Passw0rd"
export Admin__Email="admin@example.com"
export Admin__Password="Str0ngP@ss!"
export ACCEPT_EULA=Y
docker compose -f docker-compose.dev.yml up --build
```

Services:
- Gateway: http://localhost:8080
- Tasks API: http://localhost:8081
- Keycloak: http://localhost:8180
- SQL Server: localhost:1433 (volume `mssql_data`)

## Tests

### How to run
```bash
dotnet test -v n
# Filter to a specific controller:
dotnet test --filter "CommentsController"
```

### Coverage highlights
- **TasksController**: CRUD, filters (isCompleted, priority, search), sorting, visibility rules (private/team/global, multi-group membership, hidden assignees), problem mark/unmark with permissions and idempotency.
- **TaskAccessEvaluator**: full permission matrix (edit status/task/delete/problem mark/unmark) across roles, visibility scopes, and multi-group membership sets.
- **OrgsController**: org creation, invite generation, org-scoped subscription owner assignment.
- **InvitesController**: invite acceptance flow — joining org as member.
- **GroupsController**: group CRUD, member add/remove, org-scoped access enforcement.
- **UsersController**: pagination + search, public-field view for regular users, full management view for admin/owner, subscription scoping for owners, delete self-guard, task cleanup/reassignment on delete.
- **RolesController**: list roles, get user roles, assign/remove with success and error paths.
- **Auth rate limiting**: login/register (tight policy) and refresh/logout (softer policy) return 429 when limits are exceeded.
- **HealthController**: healthy path, DB-failure path (returns 503), degraded state with pending migrations (metadata includes traceId and pendingMigrations list).
- **Schema tests**: verifies all expected tables (`Subscriptions`, `Organizations`, `Groups`, `GroupMembers`, `Comments`, `OrgMembers`, `OrgInvitations`) exist after EF migrations.

### Latest test run
`dotnet test -v n` — **all 87 tests passed**.
