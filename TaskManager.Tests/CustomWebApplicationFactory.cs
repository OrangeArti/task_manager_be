using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api;
using TaskManager.Api.Authorization;
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

                // Register test authentication scheme
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

                // Seed minimal data for in-memory DB
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();

                // Seed Organization and Subscription
                if (!db.Organizations.Any(o => o.Id == 1))
                {
                    db.Organizations.Add(new Organization { Id = 1, Name = "Test Org", OwnerId = "sub-owner", CreatedAt = DateTime.UtcNow });
                    db.Subscriptions.Add(new Subscription { Id = 1, OrganizationId = 1, PlanType = "Free", CreatedAt = DateTime.UtcNow });
                }

                void EnsureUser(string id, string email)
                {
                    if (db.Users.Any(u => u.Id == id)) return;
                    db.Users.Add(new ApplicationUser
                    {
                        Id = id,
                        UserName = id,
                        NormalizedUserName = id.ToUpperInvariant(),
                        Email = email,
                        NormalizedEmail = email.ToUpperInvariant(),
                        KeycloakSubject = id,
                        SecurityStamp = Guid.NewGuid().ToString(),
                        ConcurrencyStamp = Guid.NewGuid().ToString()
                    });
                }

                EnsureUser("user1", "user1@test.local");
                EnsureUser("lead1", "lead1@test.local");
                EnsureUser("user2", "user2@test.local");
                EnsureUser("sub-owner", "sub-owner@test.local");
                db.SaveChanges(); // save users before adding FKs

                // Seed OrgMembers — sub-owner is SubscriptionOwner (matches existing test expectations)
                if (!db.OrgMembers.Any(m => m.OrganizationId == 1))
                {
                    db.OrgMembers.Add(new OrgMember { OrganizationId = 1, UserId = "user1", Role = OrgRoles.Member, JoinedAt = DateTime.UtcNow });
                    db.OrgMembers.Add(new OrgMember { OrganizationId = 1, UserId = "lead1", Role = OrgRoles.Member, JoinedAt = DateTime.UtcNow });
                    db.OrgMembers.Add(new OrgMember { OrganizationId = 1, UserId = "user2", Role = OrgRoles.Member, JoinedAt = DateTime.UtcNow });
                    db.OrgMembers.Add(new OrgMember { OrganizationId = 1, UserId = "sub-owner", Role = OrgRoles.SubscriptionOwner, JoinedAt = DateTime.UtcNow });
                }

                // Seed Group 1
                if (!db.Groups.Any(g => g.Id == 1))
                {
                    db.Groups.Add(new Group { Id = 1, Name = "Test Group", OrganizationId = 1, CreatedAt = DateTime.UtcNow });
                }
                db.SaveChanges(); // save groups before group memberships

                // Seed GroupMembers — all base users are in Group 1
                if (!db.GroupMembers.Any(gm => gm.GroupId == 1))
                {
                    db.GroupMembers.Add(new GroupMember { GroupId = 1, UserId = "user1", JoinedAt = DateTime.UtcNow });
                    db.GroupMembers.Add(new GroupMember { GroupId = 1, UserId = "lead1", JoinedAt = DateTime.UtcNow });
                    db.GroupMembers.Add(new GroupMember { GroupId = 1, UserId = "user2", JoinedAt = DateTime.UtcNow });
                    db.GroupMembers.Add(new GroupMember { GroupId = 1, UserId = "sub-owner", JoinedAt = DateTime.UtcNow });
                }

                db.SaveChanges();
            });
        }
    }
}
