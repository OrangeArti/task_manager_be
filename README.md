# Task Manager Backend

RESTful backend for tasks, teams, users, roles, and auth. Built with ASP.NET Core 9, Entity Framework Core, and Identity. Includes JWT auth, authorization policies, and extensive integration/unit test coverage.

## What it does
- Manages tasks with visibility scopes (Private, TeamPublic, GlobalPublic), assignment, priority, due dates, and problem flags.
- Supports teams and membership management.
- Provides user CRUD (admin only), roles management, and authentication (register/login/refresh/logout) with JWT + refresh tokens.
- Enforces fine-grained authorization for task actions (owner/team lead/subscription owner/admin).
- Exposes health checks for app/DB connectivity.

## Getting Started

### Prerequisites
- .NET 9 SDK
- SQL Server (for local dev) or use the included in-memory DB for tests

### Clone and restore
```bash
git clone https://github.com/your-org/task-manager-backend.git
cd task-manager-backend
dotnet restore
```

### Configuration
Create or set the following (appsettings.json or environment variables):
- `ConnectionStrings:DefaultConnection` — SQL Server connection string.
- `Jwt:Key` — strong symmetric key (32+ chars).
- `Jwt:Issuer`, `Jwt:Audience` — JWT metadata.
- `AllowedCorsOrigins` — origins for the frontend.
- `DisableHttps` — set to `true` only for local/http testing.

### Migrate and run (local SQL Server)
```bash
dotnet ef database update --project TaskManager.Api
dotnet run --project TaskManager.Api
```
App will start at http://localhost:5000 (or https://localhost:5001).

### Run with in-memory DB (tests/dev)
Set environment `ASPNETCORE_ENVIRONMENT=Testing` to use the in-memory provider (no migrations needed), then:
```bash
dotnet run --project TaskManager.Api
```

### Docker (single service)
Build and run the API container (expects a SQL Server connection string and JWT settings):
```bash
docker build -t task-manager-api -f TaskManager.Api/Dockerfile .
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal,1433;Database=TaskManagerDb;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=False;TrustServerCertificate=True" \
  -e Jwt__Key="your-32-char-secret" \
  -e Jwt__Issuer="taskmanager-api" \
  task-manager-api
```

### Docker Compose (full stack)
`docker-compose.dev.yml` starts SQL Server, Tasks API, stubbed Teams/Users services, and the API gateway:
```bash
export MSSQL_SA_PASSWORD="YourStrong!Passw0rd"
export JWT_KEY="your-32-char-secret"
export JWT_ISSUER="taskmanager-api"
export Admin__Email="admin@example.com"
export Admin__Password="Str0ngP@ss!"
export Admin__DisplayName="Admin"
export ACCEPT_EULA=Y
docker compose -f docker-compose.dev.yml up --build
```
Services:
- Gateway: http://localhost:8080
- Tasks API: http://localhost:8081
- Stub Teams: http://localhost:8082
- Stub Users: http://localhost:8083
- SQL Server: localhost:1433 (volume `mssql_data`)

## Tests

### How to run
```bash
dotnet test -v n
```

### Coverage highlights
- **AuthController**: register, login, refresh, logout (success, invalid/expired tokens, duplicate email, weak password).
- **TasksController**: CRUD, filters (isCompleted, priority, search), sorting, visibility rules (private/team/global, hidden assignees), problem mark/unmark with permissions and idempotency.
- **TeamsController**: list, get by id, create/update/delete with validation, members add/remove/list with role checks.
- **UsersController**: pagination + search, delete self-guard, task cleanup/reassignment on delete, 404 path.
- **RolesController**: list roles, get user roles, assign/remove with success and error paths.
- **TaskAccessEvaluator**: permission matrix for edit status/task/delete/problem mark/unmark across roles/scopes.
- **HealthController**: healthy path and DB-failure path (returns 503/Unhealthy).

### Latest test run
`dotnet test -v n` — **all 66 tests passed** (see summary above).
