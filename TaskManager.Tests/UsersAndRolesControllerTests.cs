using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManager.Api;
using TaskManager.Api.Authorization;
using TaskManager.Api.Controllers;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos;
using TaskManager.Api.Health;
using TaskManager.Api.Models;
using TaskManager.Shared.Health;
using Xunit;

namespace TaskManager.Tests
{
    public class UsersAndRolesControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public UsersAndRolesControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Users_GetAll_Should_Paginate()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                for (int i = 1; i <= 25; i++)
                {
                    var email = $"user{i}@test.local";
                    var user = new ApplicationUser
                    {
                        Id = $"u{i}",
                        UserName = email,
                        NormalizedUserName = email.ToUpperInvariant(),
                        Email = email,
                        NormalizedEmail = email.ToUpperInvariant(),
                        DisplayName = $"User {i}",
                        SecurityStamp = Guid.NewGuid().ToString(),
                        ConcurrencyStamp = Guid.NewGuid().ToString()
                    };
                    await userManager.CreateAsync(user, "Str0ngP@ss!");
                }
                await EnsureRoleAsync(sp, "Admin");
            });

            var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/users?page=2&pageSize=10", admin: true));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await Deserialize<PagedResult<UserDto>>(response);
            Assert.NotNull(result);
            Assert.Equal(25, result!.Total);
            Assert.Equal(2, result.Page);
            Assert.Equal(10, result.PageSize);
            Assert.Equal(10, result.Items.Count);
        }

        [Fact]
        public async Task GetUsers_Should_Return_PublicUserData_For_Regular_User()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                await userManager.CreateAsync(BuildUser("user1", "user1@test.local", "User One"), "Str0ngP@ss!");
                await userManager.CreateAsync(BuildUser("user2", "user2@test.local", "User Two"), "Str0ngP@ss!");
            });

            var request = Authorized(HttpMethod.Get, "/api/users", admin: false, userId: "user1");
            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("items", out var items));
            Assert.True(items.GetArrayLength() >= 2);

            foreach (var item in items.EnumerateArray())
            {
                Assert.True(item.TryGetProperty("id", out _));
                Assert.True(item.TryGetProperty("displayName", out _));
                Assert.False(item.TryGetProperty("email", out _)); // email should not be present for regular users
                Assert.False(item.TryGetProperty("emailConfirmed", out _));
            }
        }

        [Fact]
        public async Task Users_Delete_Should_Forbid_Cross_Subscription_For_Owner()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                await EnsureRoleAsync(sp, "SubscriptionOwner");
                await userManager.CreateAsync(BuildUser("owner1", "owner1@test.local", "Owner One", "sub-1"), "Str0ngP@ss!");
                var owner = await userManager.FindByIdAsync("owner1");
                Assert.NotNull(owner);
                await userManager.AddToRoleAsync(owner!, "SubscriptionOwner");

                await userManager.CreateAsync(BuildUser("other", "other@test.local", "Other User", "sub-2"), "Str0ngP@ss!");
            });

            var response = await _client.SendAsync(Authorized(HttpMethod.Delete, "/api/users/other", admin: false, userId: "owner1", roleOverride: "SubscriptionOwner"));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Users_GetAll_Should_Return_Only_Own_Org_For_Owner()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var db = sp.GetRequiredService<ApplicationDbContext>();
                await EnsureRoleAsync(sp, "SubscriptionOwner");

                // Create users with KeycloakSubject set (required for DB lookup in GetCurrentUserDbIdAsync)
                var owner = BuildUser("owner1", "owner1@test.local", "Owner One");
                owner.KeycloakSubject = "owner1";
                await userManager.CreateAsync(owner, "Str0ngP@ss!");
                await userManager.AddToRoleAsync(owner, "SubscriptionOwner");

                var u1 = BuildUser("u1", "u1@test.local", "User Org1");
                u1.KeycloakSubject = "u1";
                await userManager.CreateAsync(u1, "Str0ngP@ss!");

                var u2 = BuildUser("u2", "u2@test.local", "User Other");
                u2.KeycloakSubject = "u2";
                await userManager.CreateAsync(u2, "Str0ngP@ss!");

                // Create an org with owner1 and u1 as members; u2 is NOT a member
                db.Organizations.Add(new Organization { Id = 10, Name = "Test Org", OwnerId = "owner1", CreatedAt = DateTime.UtcNow });
                db.OrgMembers.Add(new OrgMember { OrganizationId = 10, UserId = "owner1", Role = OrgRoles.SubscriptionOwner, JoinedAt = DateTime.UtcNow });
                db.OrgMembers.Add(new OrgMember { OrganizationId = 10, UserId = "u1", Role = OrgRoles.Member, JoinedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            });

            var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/users", admin: false, userId: "owner1", roleOverride: "SubscriptionOwner"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await Deserialize<PagedResult<UserDto>>(response);
            Assert.NotNull(result);
            // SubscriptionOwner should only see users in their org (owner1 + u1), not u2
            Assert.Equal(2, result!.Total);
            Assert.All(result.Items, u => Assert.True(u.Id == "owner1" || u.Id == "u1"));
        }

        [Fact]
        public async Task Users_GetAll_Should_Filter_By_Search()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                await userManager.CreateAsync(BuildUser("u1", "alice@example.com", "Alice"), "Str0ngP@ss!");
                await userManager.CreateAsync(BuildUser("u2", "bob@example.com", "Bob"), "Str0ngP@ss!");
                await userManager.CreateAsync(BuildUser("u3", "carol@test.local", "Carol"), "Str0ngP@ss!");
                await EnsureRoleAsync(sp, "Admin");
            });

            var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/users?search=alice", admin: true));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await Deserialize<PagedResult<UserDto>>(response);
            Assert.NotNull(result);
            Assert.Single(result!.Items);
            Assert.Equal("alice@example.com", result.Items[0].Email);
        }

        [Fact]
        public async Task Users_GetAll_Should_Return_Full_Data_For_Admin()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                await EnsureRoleAsync(sp, "Admin");
                var admin = BuildUser("admin", "admin@test.local", "Admin User", "sub-1");
                await userManager.CreateAsync(admin, "Str0ngP@ss!");
                await userManager.AddToRoleAsync(admin, "Admin");

                await userManager.CreateAsync(BuildUser("u1", "u1@test.local", "User One", "sub-1"), "Str0ngP@ss!");
            });

            var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/users", admin: true, userId: "admin"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await Deserialize<PagedResult<UserDto>>(response);
            Assert.NotNull(result);
            Assert.True(result!.Items.Count >= 1);
            Assert.All(result.Items, u =>
            {
                Assert.False(string.IsNullOrWhiteSpace(u.Email));
                Assert.NotNull(u.DisplayName);
                // management view includes subscription id
                Assert.Equal("sub-1", u.SubscriptionId);
            });
        }

        [Fact]
        public async Task Users_Delete_Should_Reject_Self_Delete()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                await EnsureRoleAsync(sp, "Admin");
                await userManager.CreateAsync(BuildUser("admin", "admin@test.local", "Admin"), "Str0ngP@ss!");
            });

            var response = await _client.SendAsync(Authorized(HttpMethod.Delete, "/api/users/admin", admin: true, userId: "admin"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("Invalid operation", doc.RootElement.GetProperty("title").GetString());
            Assert.Contains("You cannot delete yourself.", doc.RootElement.GetProperty("detail").GetString());
        }

        [Fact]
        public async Task Users_Delete_Should_Remove_Private_And_Reassign_Shared_Tasks()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var db = sp.GetRequiredService<ApplicationDbContext>();
                await EnsureRoleAsync(sp, "Admin");

                var admin = BuildUser("admin", "admin@test.local", "Admin");
                var target = BuildUser("victim", "victim@test.local", "Victim");
                await userManager.CreateAsync(admin, "Str0ngP@ss!");
                await userManager.CreateAsync(target, "Str0ngP@ss!");

                db.Tasks.AddRange(
                    new TaskItem { Title = "private", CreatedById = "victim", VisibilityScope = TaskVisibilityScopes.Private },
                    new TaskItem { Title = "shared1", CreatedById = "victim", VisibilityScope = TaskVisibilityScopes.TeamPublic, AssignedToId = "victim", IsAssigneeVisibleToOthers = true, GroupId = 1 },
                    new TaskItem { Title = "shared2", CreatedById = "victim", VisibilityScope = TaskVisibilityScopes.GlobalPublic }
                );
                await db.SaveChangesAsync();
            });

            var response = await _client.SendAsync(Authorized(HttpMethod.Delete, "/api/users/victim", admin: true, userId: "admin"));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.False(await db.Tasks.AnyAsync(t => t.Title == "private"));

            var shared1 = await db.Tasks.FirstOrDefaultAsync(t => t.Title == "shared1");
            Assert.NotNull(shared1);
            Assert.Equal("admin", shared1!.CreatedById);
            Assert.Null(shared1.AssignedToId);

            var shared2 = await db.Tasks.FirstOrDefaultAsync(t => t.Title == "shared2");
            Assert.NotNull(shared2);
            Assert.Equal("admin", shared2!.CreatedById);
        }

        [Fact]
        public async Task Users_Delete_Should_Clear_Assignments_On_Foreign_Tasks()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var db = sp.GetRequiredService<ApplicationDbContext>();
                await EnsureRoleAsync(sp, "Admin");

                await userManager.CreateAsync(BuildUser("admin", "admin@test.local", "Admin"), "Str0ngP@ss!");
                await userManager.CreateAsync(BuildUser("victim", "victim@test.local", "Victim"), "Str0ngP@ss!");
                await userManager.CreateAsync(BuildUser("creator", "creator@test.local", "Creator"), "Str0ngP@ss!");

                db.Tasks.Add(new TaskItem
                {
                    Title = "foreign-assigned",
                    CreatedById = "creator",
                    AssignedToId = "victim",
                    VisibilityScope = TaskVisibilityScopes.TeamPublic,
                    GroupId = 1,
                    IsAssigneeVisibleToOthers = true
                });
                await db.SaveChangesAsync();
            });

            var response = await _client.SendAsync(Authorized(HttpMethod.Delete, "/api/users/victim", admin: true, userId: "admin"));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = await db.Tasks.FirstAsync(t => t.Title == "foreign-assigned");
            Assert.Null(task.AssignedToId);
            Assert.Equal("creator", task.CreatedById);
        }

        [Fact]
        public async Task Users_Delete_NonExisting_Should_Return_404()
        {
            await ResetDataAsync(async sp =>
            {
                await EnsureRoleAsync(sp, "Admin");
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                await userManager.CreateAsync(BuildUser("admin", "admin@test.local", "Admin"), "Str0ngP@ss!");
            });

            var response = await _client.SendAsync(Authorized(HttpMethod.Delete, "/api/users/missing", admin: true, userId: "admin"));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Roles_GetAll_Should_Return_Roles()
        {
            await ResetDataAsync(async sp =>
            {
                await EnsureRoleAsync(sp, "Admin");
                await EnsureRoleAsync(sp, "User");
            });

            var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/roles", admin: true));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var roles = await Deserialize<List<dynamic>>(response);
            Assert.True(roles!.Count >= 2);
        }

        [Fact]
        public async Task Roles_GetUserRoles_Should_Return_For_Existing_User_And_404_For_Missing()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                await EnsureRoleAsync(sp, "Admin");
                var user = BuildUser("user1", "user1@test.local", "User 1");
                await userManager.CreateAsync(user, "Str0ngP@ss!");
                await userManager.AddToRoleAsync(user, "Admin");
            });

            var ok = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/roles/user1", admin: true));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var payload = await ok.Content.ReadAsStringAsync();
            Assert.Contains("Admin", payload);

            var notFound = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/roles/missing", admin: true));
            Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        }

        [Fact]
        public async Task Roles_Assign_Should_Handle_Success_And_Invalid_Role()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                await EnsureRoleAsync(sp, "Admin");
                await EnsureRoleAsync(sp, "TeamLead");

                await userManager.CreateAsync(BuildUser("user1", "user1@test.local", "User 1"), "Str0ngP@ss!");
            });

            var assign = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/roles/assign", new RoleAssignRequest { UserId = "user1", RoleName = "TeamLead" }, admin: true));
            Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

            var badRole = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/roles/assign", new RoleAssignRequest { UserId = "user1", RoleName = "MissingRole" }, admin: true));
            Assert.Equal(HttpStatusCode.BadRequest, badRole.StatusCode);
            var badPayload = await badRole.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(badPayload);
            Assert.Equal("Invalid role", doc.RootElement.GetProperty("title").GetString());
        }

        [Fact]
        public async Task Roles_Remove_Should_Handle_Success_And_Missing_User()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                await EnsureRoleAsync(sp, "Admin");
                await EnsureRoleAsync(sp, "TeamLead");
                var user = BuildUser("user1", "user1@test.local", "User 1");
                await userManager.CreateAsync(user, "Str0ngP@ss!");
                await userManager.AddToRoleAsync(user, "TeamLead");
            });

            var remove = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/roles/remove", new RoleAssignRequest { UserId = "user1", RoleName = "TeamLead" }, admin: true));
            Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

            var missing = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/roles/remove", new RoleAssignRequest { UserId = "missing", RoleName = "TeamLead" }, admin: true));
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            var missingPayload = await missing.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(missingPayload);
            Assert.Equal("Not Found", doc.RootElement.GetProperty("title").GetString());
        }

        [Fact]
        public async Task Health_Should_Return_503_When_Database_Cannot_Connect()
        {
            var controller = new TaskManager.Api.Controllers.HealthController(
                new FakeHealthProbe(canConnect: false, pendingMigrations: 0, error: "fail"),
                NullLogger<TaskManager.Api.Controllers.HealthController>.Instance);

            var result = await controller.CheckHealth();

            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);

            var payload = Assert.IsType<HealthStatusDto>(objectResult.Value);
            Assert.Equal("Unhealthy", payload.Status);
        }

        [Fact]
        public async Task Health_Should_Return_503_When_Migrations_Pending()
        {
            var controller = new TaskManager.Api.Controllers.HealthController(
                new FakeHealthProbe(canConnect: true, pendingMigrations: 2, error: null),
                NullLogger<TaskManager.Api.Controllers.HealthController>.Instance);

            var result = await controller.CheckHealth();

            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);

            var payload = Assert.IsType<HealthStatusDto>(objectResult.Value);
            Assert.Equal("Degraded", payload.Status);
            Assert.Equal("Database reachable but migrations are pending.", payload.Details);
            Assert.Equal("2", payload.Metadata?["pendingMigrations"]);
        }

        private async Task ResetDataAsync(Func<IServiceProvider, Task> seeder)
        {
            using var scope = _factory.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();

            db.Tasks.RemoveRange(db.Tasks);
            db.Users.RemoveRange(db.Users);
            db.Roles.RemoveRange(db.Roles);
            await db.SaveChangesAsync();

            await seeder(sp);
        }

        private static ApplicationUser BuildUser(string id, string email, string displayName, string subscriptionId = "sub-1") =>
            new ApplicationUser
            {
                Id = id,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                DisplayName = displayName,
                SubscriptionId = subscriptionId,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };

        private static async Task EnsureRoleAsync(IServiceProvider sp, string roleName)
        {
            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        private sealed class FakeHealthProbe : IDatabaseHealthProbe
        {
            private readonly DatabaseHealthProbeResult _result;

            public FakeHealthProbe(bool canConnect, int pendingMigrations, string? error)
            {
                _result = new DatabaseHealthProbeResult(canConnect, pendingMigrations, error);
            }

            public Task<DatabaseHealthProbeResult> CheckAsync() => Task.FromResult(_result);
        }

        private HttpRequestMessage Authorized(HttpMethod method, string url, object? body = null, bool admin = false, string userId = "admin", string? roleOverride = null)
        {
            var req = new HttpRequestMessage(method, url);
            var role = roleOverride ?? (admin ? "Admin" : "User");
            req.Headers.Add("X-Test-UserId", userId);
            req.Headers.Add("X-Test-Role", role);

            if (body is not null)
            {
                req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }

            return req;
        }

        private static async Task<T?> Deserialize<T>(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
