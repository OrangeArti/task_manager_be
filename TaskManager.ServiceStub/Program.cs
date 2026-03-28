using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Keycloak.AuthServices.Authentication;
using TaskManager.Shared.Dtos.Teams;
using TaskManager.Shared.Dtos.Users;
using TaskManager.Shared.Health;

var builder = WebApplication.CreateBuilder(args);

var serviceName = builder.Configuration["SERVICE_NAME"] ?? "service-stub";
var serviceKind = builder.Configuration["SERVICE_KIND"] ?? "generic";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = $"{serviceName} API",
        Version = "v1",
        Description = $"API surface for the {serviceName} microservice ({serviceKind})."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT issued by the API Gateway. Format: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddKeycloakWebApiAuthentication(builder.Configuration, options =>
{
    options.RequireHttpsMetadata = false; // dev only
});

builder.Services.AddAuthorization();

switch (serviceKind.ToLowerInvariant())
{
    case "teams":
        builder.Services.AddSingleton<TeamStore>();
        break;
    case "users":
        builder.Services.AddSingleton<UserStore>();
        break;
}

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", $"{serviceName} API v1");
    options.RoutePrefix = "api-docs";
    options.DisplayRequestDuration();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
    {
        service = serviceName,
        kind = serviceKind,
        mock = true,
        docs = "/api-docs"
    }))
    .WithTags("Info")
    .AllowAnonymous();

app.MapGet("/health", () =>
    Results.Ok(new HealthStatusDto
    {
        Service = serviceName,
        Status = "Healthy",
        Details = $"Stub service '{serviceName}' ({serviceKind}) is running.",
        CheckedAtUtc = DateTime.UtcNow,
        Metadata = new Dictionary<string, string>
        {
            ["mockMode"] = "true",
            ["kind"] = serviceKind
        }
    }))
    .WithTags("Health")
    .AllowAnonymous();

if (string.Equals(serviceKind, "teams", StringComparison.OrdinalIgnoreCase))
{
    var group = app.MapGroup("/teams")
        .WithTags("Teams")
        .RequireAuthorization();

    group.MapGet("/", (TeamStore store) => Results.Ok(store.GetAll()))
        .WithName("GetTeams")
        .Produces<IReadOnlyCollection<TeamDto>>(StatusCodes.Status200OK);

    group.MapGet("/{id:int}", (int id, TeamStore store) =>
        store.TryGet(id, out var team)
            ? Results.Ok(team)
            : Results.NotFound())
        .WithName("GetTeamById")
        .Produces<TeamDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

    group.MapPost("/", (TeamUpsertDto dto, TeamStore store) =>
        {
            if (ValidationHelper.Validate(dto) is { } problem)
            {
                return problem;
            }

            var created = store.Create(dto);
            return Results.Created($"/teams/{created.Id}", created);
        })
        .WithName("CreateTeam")
        .Produces<TeamDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

    group.MapPut("/{id:int}", (int id, TeamUpsertDto dto, TeamStore store) =>
        {
            if (ValidationHelper.Validate(dto) is { } problem)
            {
                return problem;
            }

            return store.Update(id, dto)
                ? Results.NoContent()
                : Results.NotFound();
        })
        .WithName("UpdateTeam")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status404NotFound);

    group.MapDelete("/{id:int}", (int id, TeamStore store) =>
            store.Delete(id)
                ? Results.NoContent()
                : Results.NotFound())
        .WithName("DeleteTeam")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
}
else if (string.Equals(serviceKind, "users", StringComparison.OrdinalIgnoreCase))
{
    var group = app.MapGroup("/users")
        .WithTags("Users")
        .RequireAuthorization();

    group.MapGet("/", (UserStore store) => Results.Ok(store.GetAll()))
        .WithName("GetUsers")
        .Produces<IReadOnlyCollection<UserDto>>(StatusCodes.Status200OK);

    group.MapGet("/{id}", (string id, UserStore store) =>
        store.TryGet(id, out var user)
            ? Results.Ok(user)
            : Results.NotFound())
        .WithName("GetUserById")
        .Produces<UserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

    group.MapPost("/", (UserUpsertDto dto, UserStore store) =>
        {
            if (ValidationHelper.Validate(dto) is { } problem)
            {
                return problem;
            }

            var created = store.Create(dto);
            return Results.Created($"/users/{created.Id}", created);
        })
        .WithName("CreateUser")
        .Produces<UserDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

    group.MapPut("/{id}", (string id, UserUpsertDto dto, UserStore store) =>
        {
            if (ValidationHelper.Validate(dto) is { } problem)
            {
                return problem;
            }

            return store.Update(id, dto)
                ? Results.NoContent()
                : Results.NotFound();
        })
        .WithName("UpdateUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status404NotFound);

    group.MapDelete("/{id}", (string id, UserStore store) =>
            store.Delete(id)
                ? Results.NoContent()
                : Results.NotFound())
        .WithName("DeleteUser")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
}

app.Run();

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

internal sealed class TeamStore
{
    private readonly object _lock = new();
    private readonly Dictionary<int, TeamDto> _teams = new();
    private int _sequence = 1;

    public TeamStore()
    {
        Seed();
    }

    public IReadOnlyCollection<TeamDto> GetAll()
    {
        lock (_lock)
        {
            return _teams.Values.Select(Clone).ToArray();
        }
    }

    public bool TryGet(int id, out TeamDto? team)
    {
        lock (_lock)
        {
            if (_teams.TryGetValue(id, out var existing))
            {
                team = Clone(existing);
                return true;
            }

            team = null;
            return false;
        }
    }

    public TeamDto Create(TeamUpsertDto dto)
    {
        lock (_lock)
        {
            var entity = new TeamDto
            {
                Id = _sequence++,
                Name = dto.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description)
                    ? null
                    : dto.Description.Trim(),
                CreatedAt = DateTime.UtcNow,
                MemberCount = 0
            };

            _teams[entity.Id] = entity;
            return Clone(entity);
        }
    }

    public bool Update(int id, TeamUpsertDto dto)
    {
        lock (_lock)
        {
            if (!_teams.TryGetValue(id, out var existing))
            {
                return false;
            }

            existing.Name = dto.Name.Trim();
            existing.Description = string.IsNullOrWhiteSpace(dto.Description)
                ? null
                : dto.Description.Trim();

            return true;
        }
    }

    public bool Delete(int id)
    {
        lock (_lock)
        {
            return _teams.Remove(id);
        }
    }

    private static TeamDto Clone(TeamDto source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Description = source.Description,
        CreatedAt = source.CreatedAt,
        MemberCount = source.MemberCount
    };

    private void Seed()
    {
        Create(new TeamUpsertDto
        {
            Name = "Engineering",
            Description = "Core product engineering team."
        });

        Create(new TeamUpsertDto
        {
            Name = "QA",
            Description = "Quality assurance partnership."
        });
    }
}

internal sealed class UserStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, UserDto> _users = new(StringComparer.OrdinalIgnoreCase);
    private int _sequence = 1;

    public UserStore()
    {
        Seed();
    }

    public IReadOnlyCollection<UserDto> GetAll()
    {
        lock (_lock)
        {
            return _users.Values.Select(Clone).ToArray();
        }
    }

    public bool TryGet(string id, out UserDto? user)
    {
        lock (_lock)
        {
            if (_users.TryGetValue(id, out var existing))
            {
                user = Clone(existing);
                return true;
            }

            user = null;
            return false;
        }
    }

    public UserDto Create(UserUpsertDto dto)
    {
        lock (_lock)
        {
            var entity = new UserDto
            {
                Id = $"user-{_sequence++:D4}",
                Email = dto.Email.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName.Trim(),
                AvatarUrl = string.IsNullOrWhiteSpace(dto.AvatarUrl) ? null : dto.AvatarUrl.Trim(),
                TeamId = dto.TeamId,
                Roles = dto.Roles?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToArray()
                    ?? Array.Empty<string>()
            };

            _users[entity.Id] = entity;
            return Clone(entity);
        }
    }

    public bool Update(string id, UserUpsertDto dto)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(id, out var existing))
            {
                return false;
            }

            existing.Email = dto.Email.Trim();
            existing.DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName.Trim();
            existing.AvatarUrl = string.IsNullOrWhiteSpace(dto.AvatarUrl) ? null : dto.AvatarUrl.Trim();
            existing.TeamId = dto.TeamId;
            existing.Roles = dto.Roles?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToArray()
                ?? Array.Empty<string>();

            return true;
        }
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            return _users.Remove(id);
        }
    }

    private static UserDto Clone(UserDto source) => new()
    {
        Id = source.Id,
        Email = source.Email,
        DisplayName = source.DisplayName,
        AvatarUrl = source.AvatarUrl,
        TeamId = source.TeamId,
        Roles = source.Roles.ToArray()
    };

    private void Seed()
    {
        Create(new UserUpsertDto
        {
            Email = "alice@example.com",
            DisplayName = "Alice Johnson",
            TeamId = 1,
            Roles = new[] { "Manager" }
        });

        Create(new UserUpsertDto
        {
            Email = "bob@example.com",
            DisplayName = "Bob Smith",
            TeamId = 1,
            Roles = new[] { "Contributor" }
        });
    }
}
