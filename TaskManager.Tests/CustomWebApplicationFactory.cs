using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api;
using TaskManager.Api.Data;
using Microsoft.AspNetCore.Authentication;
using TaskManager.Api.Models;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TaskManager.Tests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"TestDb-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");


            builder.ConfigureServices(services =>
            {
                
                // Подключаем тестовую схему аутентификации
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, options => { });

                var descriptors = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>))
                    .ToList();

                foreach (var d in descriptors)
                {
                    services.Remove(d);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options
                        .UseInMemoryDatabase(_dbName)
                        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });

                // Сеем минимальные данные (команда + пользователи) для in-memory БД
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();


                if (!db.Teams.Any(t => t.Id == 1))
                {
                    db.Teams.Add(new Team { Id = 1, Name = "Team 1" });
                }

                void EnsureUser(string id, string email, int? teamId, string subscriptionId = "sub-1")
                {
                    if (db.Users.Any(u => u.Id == id))
                        return;

                    db.Users.Add(new ApplicationUser
                    {
                        Id = id,
                        UserName = id,
                        NormalizedUserName = id.ToUpperInvariant(),
                        Email = email,
                        NormalizedEmail = email.ToUpperInvariant(),
                        TeamId = teamId,
                        SubscriptionId = subscriptionId,
                        KeycloakSubject = id, // matches the "sub" claim emitted by TestAuthHandler
                        SecurityStamp = Guid.NewGuid().ToString(),
                        ConcurrencyStamp = Guid.NewGuid().ToString()
                    });
                }

                EnsureUser("user1", "user1@test.local", 1);
                EnsureUser("lead1", "lead1@test.local", 1);
                EnsureUser("user2", "user2@test.local", 1);
                EnsureUser("sub-owner", "sub-owner@test.local", 1);

                db.SaveChanges();
            });
        }
    }
}
