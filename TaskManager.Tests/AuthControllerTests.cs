using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos;
using TaskManager.Api.Models;
using TaskManager.Api.Services;
using Xunit;

namespace TaskManager.Tests
{
    public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        private const string ExistingEmail = "login@test.local";
        private const string ExistingPassword = "StrongP@ssw0rd1";

        public AuthControllerTests(CustomWebApplicationFactory baseFactory)
        {
            _factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace real JWT generator with a predictable fake.
                    var jwtDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IJwtTokenService));
                    if (jwtDescriptor is not null)
                    {
                        services.Remove(jwtDescriptor);
                    }
                    services.AddSingleton<IJwtTokenService, FakeJwtTokenService>();

                    // Seed roles, an existing user, and refresh tokens.
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    EnsureRoleAsync(roleManager, "User").GetAwaiter().GetResult();

                    var user = userManager.FindByEmailAsync(ExistingEmail).GetAwaiter().GetResult();
                    if (user is null)
                    {
                        user = new ApplicationUser
                        {
                            UserName = ExistingEmail,
                            Email = ExistingEmail,
                            DisplayName = "Login User"
                        };

                        userManager.CreateAsync(user, ExistingPassword).GetAwaiter().GetResult();
                        userManager.AddToRoleAsync(user, "User").GetAwaiter().GetResult();
                    }

                    if (!db.RefreshTokens.Any(r => r.Token == "valid-refresh"))
                    {
                        db.RefreshTokens.Add(new RefreshToken
                        {
                            Token = "valid-refresh",
                            UserId = user.Id,
                            ExpiresAt = DateTime.UtcNow.AddDays(1),
                            IsRevoked = false
                        });
                    }

                    if (!db.RefreshTokens.Any(r => r.Token == "expired-refresh"))
                    {
                        db.RefreshTokens.Add(new RefreshToken
                        {
                            Token = "expired-refresh",
                            UserId = user.Id,
                            ExpiresAt = DateTime.UtcNow.AddDays(-1),
                            IsRevoked = false
                        });
                    }

                    if (!db.RefreshTokens.Any(r => r.Token == "logout-refresh"))
                    {
                        db.RefreshTokens.Add(new RefreshToken
                        {
                            Token = "logout-refresh",
                            UserId = user.Id,
                            ExpiresAt = DateTime.UtcNow.AddDays(1),
                            IsRevoked = false
                        });
                    }

                    db.SaveChanges();
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task Register_Should_Create_User_And_Return_Tokens()
        {
            var payload = new
            {
                email = "newuser@test.local",
                password = "Str0ngP@ss!",
                displayName = "New User"
            };

            var response = await _client.PostAsync("/api/auth/register", JsonContent(payload));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            var auth = JsonSerializer.Deserialize<AuthResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(auth);
            Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
            Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
            Assert.Equal(payload.email, auth.Email);

            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var created = await userManager.FindByEmailAsync(payload.email);
            Assert.NotNull(created);
            Assert.True(await userManager.IsInRoleAsync(created!, "User"));
        }

        [Fact]
        public async Task Register_With_Existing_Email_Should_Return_Conflict()
        {
            var payload = new
            {
                email = ExistingEmail,
                password = ExistingPassword,
                displayName = "Dup"
            };

            var response = await _client.PostAsync("/api/auth/register", JsonContent(payload));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var message = await response.Content.ReadAsStringAsync();
            Assert.Contains("already exists", message);
        }

        [Fact]
        public async Task Register_With_Invalid_Password_Should_Return_BadRequest()
        {
            var payload = new
            {
                email = "weak@test.local",
                password = "123",
                displayName = "Weak"
            };

            var response = await _client.PostAsync("/api/auth/register", JsonContent(payload));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_Should_Return_Tokens_For_Valid_Credentials()
        {
            var payload = new { email = ExistingEmail, password = ExistingPassword };

            var response = await _client.PostAsync("/api/auth/login", JsonContent(payload));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var auth = await Deserialize<AuthResponse>(response);
            Assert.NotNull(auth);
            Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
            Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        }

        [Fact]
        public async Task Login_With_Wrong_Password_Should_Return_Unauthorized()
        {
            var payload = new { email = ExistingEmail, password = "WrongP@ss!" };

            var response = await _client.PostAsync("/api/auth/login", JsonContent(payload));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var message = await response.Content.ReadAsStringAsync();
            Assert.Contains("Invalid credentials", message);
        }

        [Fact]
        public async Task Login_With_NonExisting_Email_Should_Return_Unauthorized()
        {
            var payload = new { email = "missing@test.local", password = ExistingPassword };

            var response = await _client.PostAsync("/api/auth/login", JsonContent(payload));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Refresh_With_Valid_Token_Should_Return_New_Tokens_And_Revoke_Old()
        {
            var response = await _client.PostAsync("/api/auth/refresh", PlainJson("valid-refresh"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var auth = await Deserialize<AuthResponse>(response);
            Assert.NotNull(auth);
            Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
            Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
            Assert.NotEqual("valid-refresh", auth.RefreshToken);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var old = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == "valid-refresh");
            Assert.NotNull(old);
            Assert.True(old!.IsRevoked);
            Assert.True(await db.RefreshTokens.AnyAsync(r => r.Token == auth.RefreshToken && !r.IsRevoked));
        }

        [Fact]
        public async Task Refresh_With_Invalid_Token_Should_Return_Unauthorized()
        {
            var response = await _client.PostAsync("/api/auth/refresh", PlainJson("unknown-token"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var message = await response.Content.ReadAsStringAsync();
            Assert.Contains("Invalid refresh token", message);
        }

        [Fact]
        public async Task Refresh_With_Expired_Token_Should_Return_Unauthorized()
        {
            var response = await _client.PostAsync("/api/auth/refresh", PlainJson("expired-refresh"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Logout_With_Active_Token_Should_Revoke_And_Return_Ok()
        {
            var response = await _client.PostAsync("/api/auth/logout", PlainJson("logout-refresh"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var token = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == "logout-refresh");
            Assert.NotNull(token);
            Assert.True(token!.IsRevoked);
        }

        [Fact]
        public async Task Logout_With_Unknown_Token_Should_Return_NotFound()
        {
            var response = await _client.PostAsync("/api/auth/logout", PlainJson("missing-refresh"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var message = await response.Content.ReadAsStringAsync();
            Assert.Contains("not found", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Login_Should_Hit_RateLimit_After_5_Attempts()
        {
            await ResetDataAsync(async sp =>
            {
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
                await EnsureRoleAsync(roleManager, "User");
                await userManager.CreateAsync(BuildUser("login-user", "login-user@test.local", "Login User"), "Str0ngP@ss!");
            });

            HttpResponseMessage? lastResponse = null;
            for (int i = 1; i <= 6; i++)
            {
                var payload = new { email = "login-user@test.local", password = "Str0ngP@ss!" };
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
                {
                    Content = JsonContent(payload)
                };
                request.Headers.Add("X-Test-UserId", "login-user");

                lastResponse = await _client.SendAsync(request);
            }

            Assert.NotNull(lastResponse);
            Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
        }

        [Fact]
        public async Task Register_Should_Hit_RateLimit_After_5_Attempts()
        {
            HttpResponseMessage? lastResponse = null;
            for (int i = 1; i <= 6; i++)
            {
                var payload = new
                {
                    email = $"new{i}@test.local",
                    password = "Str0ngP@ss!",
                    displayName = "New User"
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
                {
                    Content = JsonContent(payload)
                };
                request.Headers.Add("X-Test-UserId", "rate-user");

                lastResponse = await _client.SendAsync(request);
            }

            Assert.NotNull(lastResponse);
            Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
        }

        private static StringContent JsonContent(object value) =>
            new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

        private static StringContent PlainJson(string value) =>
            new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

        private static async Task<T?> Deserialize<T>(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string role)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        private static ApplicationUser BuildUser(string id, string email, string displayName) =>
            new ApplicationUser
            {
                Id = id,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                DisplayName = displayName,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };

        private async Task ResetDataAsync(Func<IServiceProvider, Task> seeder)
        {
            using var scope = _factory.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();

            db.Tasks.RemoveRange(db.Tasks);
            db.Users.RemoveRange(db.Users);
            db.Roles.RemoveRange(db.Roles);
            db.RefreshTokens.RemoveRange(db.RefreshTokens);
            await db.SaveChangesAsync();

            await seeder(sp);
        }

        private sealed class FakeJwtTokenService : IJwtTokenService
        {
            public string CreateToken(ApplicationUser user, IList<string> roles) =>
                $"fake-jwt-{user.Id}";
        }
    }
}
