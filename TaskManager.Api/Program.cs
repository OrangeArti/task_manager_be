using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TaskManager.Api.Models;
using Microsoft.OpenApi.Models; // required for Swagger security
using TaskManager.Api;
using TaskManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using TaskManager.Api.Authorization.Handlers;
using TaskManager.Shared.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

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

// JWT auth shared configuration
builder.Services
    .Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtOptions = builder.Configuration.GetJwtOptions();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.Zero
        };
    });

// Register Authorization (must run before UseAuthorization)
builder.Services.AddAuthorization(
    options =>
    {
        Policies.RegisterPolicies(options);
    }
);

builder.Services.AddScoped<IAuthorizationHandler, TaskReadHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TaskWriteHandler>();

builder.Services.AddScoped<TaskManager.Api.Services.IJwtTokenService, TaskManager.Api.Services.JwtTokenService>();
builder.Services.AddScoped<ITeamService, TeamService>();

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

    // Seed roles and default admin
    if (!builder.Environment.IsEnvironment("Testing"))
    {
         await IdentitySeeder.SeedAsync(app.Services, app.Configuration);
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

app.UseCors("FrontendDev");

app.UseAuthentication();   // must be called before UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.Run();
public partial class Program { }
