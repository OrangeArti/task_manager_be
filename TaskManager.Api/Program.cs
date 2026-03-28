using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using TaskManager.Api.Models;
using Microsoft.OpenApi.Models; // required for Swagger security
using TaskManager.Api;
using TaskManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using TaskManager.Api.Authorization.Handlers;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using TaskManager.Api.Health;
using Microsoft.AspNetCore.HttpLogging;
using System.Diagnostics;
using System.Security.Claims;
using Keycloak.AuthServices.Authentication;
using TaskManager.Api.Auth;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Logging: enable scopes and structured console output
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

// Controllers
builder.Services.AddControllers();

// Swagger + JWT security
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "TaskManager API",
        Version = "v1",
        Description = "MVP API for Task Manager (Auth, Tasks, Health)."
    });

    // XML comments (if the file exists)
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // JWT in Swagger (Authorize button)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter the JWT token in the format: Bearer {your token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
                },
                Scheme = "Bearer",
                Name = "Authorization",
                In = ParameterLocation.Header
            },
            Array.Empty<string>()
        }
    });
});

// DbContext + retries
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure()
        )
    );
}

// Identity + EF stores
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddHttpContextAccessor();

// Suppress Identity cookie redirects — this is an API, not a browser app.
// Without this, unauthenticated requests get a 302 → /Account/Login instead of 401.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// Keycloak OIDC authentication — validates RS256 tokens via JWKS discovery
builder.Services.AddKeycloakWebApiAuthentication(builder.Configuration, options =>
{
    options.RequireHttpsMetadata = false; // dev only — remove for production
});

// Map realm_access.roles JWT claim to ClaimTypes.Role for User.IsInRole() support
builder.Services.AddTransient<IClaimsTransformation, KeycloakClaimsTransformer>();

// Register Authorization (must run before UseAuthorization)
builder.Services.AddAuthorization(
    options =>
    {
        Policies.RegisterPolicies(options);
    }
);

builder.Services.AddScoped<IAuthorizationHandler, TaskReadHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TaskWriteHandler>();

builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IDatabaseHealthProbe, EfDatabaseHealthProbe>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("AuthTight", context =>
    {
        var partitionKey = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? context.Request.Headers["X-Test-UserId"].FirstOrDefault()
                           ?? context.Connection.RemoteIpAddress?.ToString()
                           ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    options.AddPolicy("AuthSoft", context =>
    {
        var partitionKey = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? context.Request.Headers["X-Test-UserId"].FirstOrDefault()
                           ?? context.Connection.RemoteIpAddress?.ToString()
                           ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
                            | HttpLoggingFields.RequestPath
                            | HttpLoggingFields.ResponseStatusCode
                            | HttpLoggingFields.Duration;
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("AllowedCorsOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "Authorization")
            .WithExposedHeaders("Location");
    });
});

var disableHttps = builder.Configuration.GetValue<bool>("DisableHttps");

var app = builder.Build();

// Auto-migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        await db.Database.MigrateAsync();
    }
}

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskManager API v1");
        c.RoutePrefix = "swagger";
    });
}

if (!disableHttps)
{
    app.UseHttpsRedirection();
}

app.UseHttpLogging();

// Correlation/trace id propagation
app.Use(async (context, next) =>
{
    var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
    context.Response.Headers["X-Trace-Id"] = traceId;

    var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
    using (loggerFactory.CreateLogger("RequestScope").BeginScope(new Dictionary<string, object?>
    {
        ["TraceId"] = traceId,
        ["UserId"] = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        ["SubscriptionId"] = context.User?.FindFirst("subscription_id")?.Value
    }))
    {
        await next();
    }
});

app.UseCors("FrontendDev");

app.UseAuthentication();   // must be called before UseAuthorization
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
public partial class Program { }
