using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TaskManager.Shared.Dtos.Tasks;
using TaskManager.Shared.Dtos.Teams;
using TaskManager.Shared.Dtos.Users;
using TaskManager.Shared.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection("Gateway"));
builder.Services.AddSingleton<MockDataStore>();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Gateway:Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services
    .AddAuthentication("Basic")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", static options =>
    {
        options.TimeProvider ??= TimeProvider.System;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

var apiGroup = app.MapGroup("/api")
    .WithOpenApi()
    .RequireAuthorization();

apiGroup.MapGet("/health", (MockDataStore store, IOptions<GatewayOptions> options) =>
    Results.Ok(store.GetHealthSnapshot(options.Value)))
    .WithName("GetAggregatedHealth")
    .WithTags("Health");

var tasksGroup = apiGroup.MapGroup("/tasks")
    .WithTags("Tasks");

tasksGroup.MapGet("/", (MockDataStore store) => Results.Ok(store.GetTasks()));

tasksGroup.MapGet("/{id:int}", (MockDataStore store, int id) =>
    store.TryGetTask(id, out var task)
        ? Results.Ok(task)
        : Results.NotFound());

tasksGroup.MapPost("/", (TaskUpsertDto dto, MockDataStore store, ClaimsPrincipal principal) =>
{
    if (ValidationHelper.Validate(dto) is { } validationProblem)
    {
        return validationProblem;
    }

    var created = store.CreateTask(dto, principal.Identity?.Name ?? "frontend-client");
    return Results.Created($"/api/tasks/{created.Id}", created);
});

tasksGroup.MapPut("/{id:int}", (int id, TaskUpsertDto dto, MockDataStore store) =>
{
    if (ValidationHelper.Validate(dto) is { } validationProblem)
    {
        return validationProblem;
    }

    return store.UpdateTask(id, dto)
        ? Results.NoContent()
        : Results.NotFound();
});

tasksGroup.MapDelete("/{id:int}", (int id, MockDataStore store) =>
    store.DeleteTask(id) ? Results.NoContent() : Results.NotFound());

var teamsGroup = apiGroup.MapGroup("/teams")
    .WithTags("Teams");

teamsGroup.MapGet("/", (MockDataStore store) => Results.Ok(store.GetTeams()));

teamsGroup.MapGet("/{id:int}", (MockDataStore store, int id) =>
    store.TryGetTeam(id, out var team)
        ? Results.Ok(team)
        : Results.NotFound());

teamsGroup.MapPost("/", (TeamUpsertDto dto, MockDataStore store) =>
{
    if (ValidationHelper.Validate(dto) is { } validationProblem)
    {
        return validationProblem;
    }

    var created = store.CreateTeam(dto);
    return Results.Created($"/api/teams/{created.Id}", created);
});

teamsGroup.MapPut("/{id:int}", (int id, TeamUpsertDto dto, MockDataStore store) =>
{
    if (ValidationHelper.Validate(dto) is { } validationProblem)
    {
        return validationProblem;
    }

    return store.UpdateTeam(id, dto)
        ? Results.NoContent()
        : Results.NotFound();
});

teamsGroup.MapDelete("/{id:int}", (int id, MockDataStore store) =>
    store.DeleteTeam(id) ? Results.NoContent() : Results.NotFound());

var usersGroup = apiGroup.MapGroup("/users")
    .WithTags("Users");

usersGroup.MapGet("/", (MockDataStore store) => Results.Ok(store.GetUsers()));

usersGroup.MapGet("/{id}", (MockDataStore store, string id) =>
    store.TryGetUser(id, out var user)
        ? Results.Ok(user)
        : Results.NotFound());

usersGroup.MapPost("/", (UserUpsertDto dto, MockDataStore store) =>
{
    if (ValidationHelper.Validate(dto) is { } validationProblem)
    {
        return validationProblem;
    }

    var created = store.CreateUser(dto);
    return Results.Created($"/api/users/{created.Id}", created);
});

usersGroup.MapPut("/{id}", (string id, UserUpsertDto dto, MockDataStore store) =>
{
    if (ValidationHelper.Validate(dto) is { } validationProblem)
    {
        return validationProblem;
    }

    return store.UpdateUser(id, dto)
        ? Results.NoContent()
        : Results.NotFound();
});

usersGroup.MapDelete("/{id}", (string id, MockDataStore store) =>
    store.DeleteUser(id) ? Results.NoContent() : Results.NotFound());

app.Run();

internal sealed class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IOptionsMonitor<GatewayOptions> _optionsMonitor;

    public BasicAuthenticationHandler(
        IOptionsMonitor<GatewayOptions> optionsMonitor,
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _optionsMonitor = optionsMonitor;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));
        }

        if (!AuthenticationHeaderValue.TryParse(Request.Headers["Authorization"], out var headerValue))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header."));
        }

        if (!"Basic".Equals(headerValue.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid authentication scheme."));
        }

        if (string.IsNullOrWhiteSpace(headerValue.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing credentials."));
        }

        string decodedCredentials;
        try
        {
            decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(headerValue.Parameter));
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Base64 credentials."));
        }

        var tokens = decodedCredentials.Split(':', 2);
        if (tokens.Length != 2)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid credential format."));
        }

        var configured = _optionsMonitor.CurrentValue.Auth;
        if (!string.Equals(tokens[0], configured.Username, StringComparison.Ordinal) ||
            !string.Equals(tokens[1], configured.Password, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid username or password."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, tokens[0]),
            new Claim(ClaimTypes.Name, tokens[0])
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers["WWW-Authenticate"] = "Basic realm=\"TaskManager\"";
        return base.HandleChallengeAsync(properties);
    }
}

internal static class ValidationHelper
{
    public static IResult? Validate<T>(T instance)
    {
        if (instance is null)
        {
            return Results.BadRequest("Payload is required.");
        }

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(instance);
        if (Validator.TryValidateObject(instance, context, validationResults, validateAllProperties: true))
        {
            return null;
        }

        var errors = validationResults
            .GroupBy(result => result.MemberNames.FirstOrDefault() ?? string.Empty)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(result => result.ErrorMessage ?? "Validation error.")
                    .ToArray());

        return Results.ValidationProblem(errors);
    }
}

internal sealed class MockDataStore
{
    private static readonly string[] VisibleScopes = ["Private", "TeamPublic", "GlobalPublic"];

    private readonly object _lock = new();
    private readonly Dictionary<int, TaskDto> _tasks = new();
    private readonly Dictionary<int, TeamDto> _teams = new();
    private readonly Dictionary<string, UserDto> _users = new(StringComparer.OrdinalIgnoreCase);

    private int _taskSequence = 1;
    private int _teamSequence = 1;
    private int _userSequence = 1;

    public MockDataStore()
    {
        Seed();
    }

    public IReadOnlyCollection<TaskDto> GetTasks()
    {
        lock (_lock)
        {
            return _tasks.Values.Select(CloneTask).ToArray();
        }
    }

    public bool TryGetTask(int id, out TaskDto? task)
    {
        lock (_lock)
        {
            if (_tasks.TryGetValue(id, out var stored))
            {
                task = CloneTask(stored);
                return true;
            }

            task = null;
            return false;
        }
    }

    public TaskDto CreateTask(TaskUpsertDto dto, string createdBy)
    {
        var normalizedScope = NormalizeScope(dto.VisibilityScope);

        lock (_lock)
        {
            var task = new TaskDto
            {
                Id = _taskSequence++,
                Title = dto.Title,
                Description = dto.Description,
                DueDate = dto.DueDate,
                Priority = dto.Priority,
                AssignedToId = dto.AssignedToId,
                TeamId = dto.TeamId,
                IsCompleted = false,
                VisibilityScope = normalizedScope,
                CreatedAt = DateTime.UtcNow,
                CreatedById = createdBy
            };

            _tasks[task.Id] = task;
            return CloneTask(task);
        }
    }

    public bool UpdateTask(int id, TaskUpsertDto dto)
    {
        lock (_lock)
        {
            if (!_tasks.TryGetValue(id, out var existing))
            {
                return false;
            }

            existing.Title = dto.Title;
            existing.Description = dto.Description;
            existing.DueDate = dto.DueDate;
            existing.Priority = dto.Priority;
            existing.AssignedToId = dto.AssignedToId;
            existing.TeamId = dto.TeamId;
            existing.VisibilityScope = NormalizeScope(dto.VisibilityScope);

            return true;
        }
    }

    public bool DeleteTask(int id)
    {
        lock (_lock)
        {
            return _tasks.Remove(id);
        }
    }

    public IReadOnlyCollection<TeamDto> GetTeams()
    {
        lock (_lock)
        {
            return _teams.Values.Select(CloneTeam).ToArray();
        }
    }

    public bool TryGetTeam(int id, out TeamDto? team)
    {
        lock (_lock)
        {
            if (_teams.TryGetValue(id, out var stored))
            {
                team = CloneTeam(stored);
                return true;
            }

            team = null;
            return false;
        }
    }

    public TeamDto CreateTeam(TeamUpsertDto dto)
    {
        lock (_lock)
        {
            var team = new TeamDto
            {
                Id = _teamSequence++,
                Name = dto.Name,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                MemberCount = 0
            };

            _teams[team.Id] = team;
            return CloneTeam(team);
        }
    }

    public bool UpdateTeam(int id, TeamUpsertDto dto)
    {
        lock (_lock)
        {
            if (!_teams.TryGetValue(id, out var existing))
            {
                return false;
            }

            existing.Name = dto.Name;
            existing.Description = dto.Description;
            return true;
        }
    }

    public bool DeleteTeam(int id)
    {
        lock (_lock)
        {
            if (!_teams.Remove(id))
            {
                return false;
            }

            foreach (var user in _users.Values.Where(u => u.TeamId == id))
            {
                user.TeamId = null;
            }

            RecalculateTeamMembers();
            return true;
        }
    }

    public IReadOnlyCollection<UserDto> GetUsers()
    {
        lock (_lock)
        {
            return _users.Values.Select(CloneUser).ToArray();
        }
    }

    public bool TryGetUser(string id, out UserDto? user)
    {
        lock (_lock)
        {
            if (_users.TryGetValue(id, out var stored))
            {
                user = CloneUser(stored);
                return true;
            }

            user = null;
            return false;
        }
    }

    public UserDto CreateUser(UserUpsertDto dto)
    {
        lock (_lock)
        {
            var user = new UserDto
            {
                Id = $"user-{_userSequence++:D4}",
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                AvatarUrl = dto.AvatarUrl,
                TeamId = dto.TeamId,
                Roles = dto.Roles?.ToArray() ?? Array.Empty<string>()
            };

            _users[user.Id] = user;
            RecalculateTeamMembers();

            return CloneUser(user);
        }
    }

    public bool UpdateUser(string id, UserUpsertDto dto)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(id, out var existing))
            {
                return false;
            }

            existing.Email = dto.Email;
            existing.DisplayName = dto.DisplayName;
            existing.AvatarUrl = dto.AvatarUrl;
            existing.TeamId = dto.TeamId;
            existing.Roles = dto.Roles?.ToArray() ?? Array.Empty<string>();

            RecalculateTeamMembers();
            return true;
        }
    }

    public bool DeleteUser(string id)
    {
        lock (_lock)
        {
            if (!_users.Remove(id))
            {
                return false;
            }

            RecalculateTeamMembers();
            return true;
        }
    }

    public IReadOnlyCollection<HealthStatusDto> GetHealthSnapshot(GatewayOptions options)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var statuses = new List<HealthStatusDto>
            {
                new()
                {
                    Service = "gateway",
                    Status = "Healthy",
                    Details = "API Gateway mock routing operational.",
                    CheckedAtUtc = now,
                    Metadata = new Dictionary<string, string>
                    {
                        ["mockMode"] = "true",
                        ["routeCount"] = options.Services.Count.ToString()
                    }
                },
                new()
                {
                    Service = "tasks",
                    Status = _tasks.Count > 0 ? "Healthy" : "Degraded",
                    Details = $"{_tasks.Count} task(s) available in mock store.",
                    CheckedAtUtc = now,
                    Metadata = BuildMetadata(options, "tasks")
                },
                new()
                {
                    Service = "teams",
                    Status = _teams.Count > 0 ? "Healthy" : "Degraded",
                    Details = $"{_teams.Count} team(s) available in mock store.",
                    CheckedAtUtc = now,
                    Metadata = BuildMetadata(options, "teams")
                },
                new()
                {
                    Service = "users",
                    Status = _users.Count > 0 ? "Healthy" : "Degraded",
                    Details = $"{_users.Count} user(s) available in mock store.",
                    CheckedAtUtc = now,
                    Metadata = BuildMetadata(options, "users")
                }
            };

            return statuses;
        }
    }

    private static TaskDto CloneTask(TaskDto source) => new()
    {
        Id = source.Id,
        Title = source.Title,
        Description = source.Description,
        DueDate = source.DueDate,
        Priority = source.Priority,
        AssignedToId = source.AssignedToId,
        TeamId = source.TeamId,
        IsCompleted = source.IsCompleted,
        CreatedAt = source.CreatedAt,
        CreatedById = source.CreatedById,
        VisibilityScope = source.VisibilityScope
    };

    private static TeamDto CloneTeam(TeamDto source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Description = source.Description,
        CreatedAt = source.CreatedAt,
        MemberCount = source.MemberCount
    };

    private static UserDto CloneUser(UserDto source) => new()
    {
        Id = source.Id,
        Email = source.Email,
        DisplayName = source.DisplayName,
        AvatarUrl = source.AvatarUrl,
        TeamId = source.TeamId,
        Roles = source.Roles.ToArray()
    };

    private static string NormalizeScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return "Private";
        }

        foreach (var option in VisibleScopes)
        {
            if (string.Equals(option, scope, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return "Private";
    }

    private static IDictionary<string, string> BuildMetadata(GatewayOptions options, string key)
    {
        if (options.Services.TryGetValue(key, out var service))
        {
            return new Dictionary<string, string>
            {
                ["baseUrl"] = service.BaseUrl,
                ["healthPath"] = service.HealthPath ?? "/health",
                ["mockMode"] = "true"
            };
        }

        return new Dictionary<string, string>
        {
            ["mockMode"] = "true"
        };
    }

    private void Seed()
    {
        var engineering = new TeamDto
        {
            Id = _teamSequence++,
            Name = "Engineering",
            Description = "Core product engineering team.",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            MemberCount = 0
        };

        var qa = new TeamDto
        {
            Id = _teamSequence++,
            Name = "QA",
            Description = "Quality assurance partnership.",
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            MemberCount = 0
        };

        _teams[engineering.Id] = engineering;
        _teams[qa.Id] = qa;

        var alice = new UserDto
        {
            Id = $"user-{_userSequence++:D4}",
            Email = "alice@example.com",
            DisplayName = "Alice",
            TeamId = engineering.Id,
            Roles = new[] { "Admin" }
        };

        var bob = new UserDto
        {
            Id = $"user-{_userSequence++:D4}",
            Email = "bob@example.com",
            DisplayName = "Bob",
            TeamId = engineering.Id,
            Roles = new[] { "Manager" }
        };

        var charlie = new UserDto
        {
            Id = $"user-{_userSequence++:D4}",
            Email = "charlie@example.com",
            DisplayName = "Charlie",
            TeamId = qa.Id,
            Roles = new[] { "Contributor" }
        };

        _users[alice.Id] = alice;
        _users[bob.Id] = bob;
        _users[charlie.Id] = charlie;

        RecalculateTeamMembers();

        var task1 = new TaskDto
        {
            Id = _taskSequence++,
            Title = "Create microservice architecture blueprint",
            Description = "Document service boundaries and deployment plan.",
            DueDate = DateTime.UtcNow.AddDays(5),
            Priority = 2,
            AssignedToId = bob.Id,
            TeamId = engineering.Id,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            CreatedById = alice.Id,
            VisibilityScope = "TeamPublic"
        };

        var task2 = new TaskDto
        {
            Id = _taskSequence++,
            Title = "Prepare QA automation backlog",
            Description = "List top regression scenarios for the new services.",
            DueDate = DateTime.UtcNow.AddDays(7),
            Priority = 3,
            AssignedToId = charlie.Id,
            TeamId = qa.Id,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedById = alice.Id,
            VisibilityScope = "GlobalPublic"
        };

        _tasks[task1.Id] = task1;
        _tasks[task2.Id] = task2;
    }

    private void RecalculateTeamMembers()
    {
        foreach (var team in _teams.Values)
        {
            team.MemberCount = _users.Values.Count(u => u.TeamId == team.Id);
        }
    }
}

internal sealed class GatewayOptions
{
    public GatewayAuthOptions Auth { get; set; } = new();

    public IDictionary<string, GatewayServiceOptions> Services { get; set; }
        = new Dictionary<string, GatewayServiceOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["tasks"] = new() { Name = "tasks-service", BaseUrl = "http://tasks-service:8080", HealthPath = "/health" },
            ["teams"] = new() { Name = "teams-service", BaseUrl = "http://teams-service:8080", HealthPath = "/health" },
            ["users"] = new() { Name = "users-service", BaseUrl = "http://users-service:8080", HealthPath = "/health" }
        };
}

internal sealed class GatewayAuthOptions
{
    public string Username { get; set; } = "frontend";
    public string Password { get; set; } = "frontend-secret";
}

internal sealed class GatewayServiceOptions
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string HealthPath { get; set; } = "/health";
}
