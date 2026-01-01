using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Dtos;
using TaskManager.Api.Services;
using Xunit;

namespace TaskManager.Tests
{
    public class TeamsControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly FakeTeamService _service;
        private readonly HttpClient _client;

        public TeamsControllerTests(CustomWebApplicationFactory factory)
        {
            // Подменяем ITeamService на фейковый, чтобы контролировать вывод.
            _service = new FakeTeamService();

            var customizedFactory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var existing = services.SingleOrDefault(d => d.ServiceType == typeof(ITeamService));
                    if (existing is not null)
                        services.Remove(existing);

                    services.AddSingleton<ITeamService>(_service);
                });
            });

            _client = customizedFactory.CreateClient();
        }

        [Fact]
        public async Task GetAllAsync_Should_Return_200_And_List_Of_Teams()
        {
            _service.Reset();
            var request = Authorized(HttpMethod.Get, "/api/teams");

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            var teams = JsonSerializer.Deserialize<List<TeamDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(teams);
            Assert.Equal(2, teams!.Count);
            Assert.Contains(teams, t => t.Id == 1 && t.Name == "Alpha" && t.MemberCount == 2);
            Assert.Contains(teams, t => t.Id == 2 && t.Name == "Beta" && t.MemberCount == 1);
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_Existing_Team()
        {
            _service.Reset();
            var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/teams/by-id/1"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var team = await Deserialize<TeamDto>(response);
            Assert.NotNull(team);
            Assert.Equal("Alpha", team!.Name);
            Assert.Equal(2, team.MemberCount);
        }

        [Fact]
        public async Task GetByIdAsync_Should_Return_404_For_Unknown_Team()
        {
            _service.Reset();
            var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/teams/by-id/999"));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateAsync_Should_Return_201_And_Location()
        {
            _service.Reset();
            var payload = new { name = "New team", description = "Desc" };
            var request = Authorized(HttpMethod.Post, "/api/teams", payload);

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Contains("/api/teams/by-id/", response.Headers.Location!.ToString(), System.StringComparison.OrdinalIgnoreCase);

            var team = await Deserialize<TeamDto>(response);
            Assert.NotNull(team);
            Assert.Equal("New team", team!.Name);
        }

        [Fact]
        public async Task CreateAsync_With_Empty_Name_Should_Return_400()
        {
            _service.Reset();
            var payload = new { name = "", description = "Desc" };
            var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/teams", payload));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("The Name field is required.", content);
        }

        [Fact]
        public async Task UpdateAsync_Should_Update_Name_And_Description()
        {
            _service.Reset();
            var payload = new { name = "Updated", description = "New desc" };
            var response = await _client.SendAsync(Authorized(HttpMethod.Put, "/api/teams/1", payload));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var team = await Deserialize<TeamDto>(response);
            Assert.NotNull(team);
            Assert.Equal("Updated", team!.Name);
            Assert.Equal("New desc", team.Description);
        }

        [Fact]
        public async Task UpdateAsync_With_No_Fields_Should_Return_400()
        {
            _service.Reset();
            var payload = new { };
            var response = await _client.SendAsync(Authorized(HttpMethod.Put, "/api/teams/1", payload));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("At least one field must be provided", content);
        }

        [Fact]
        public async Task UpdateAsync_With_Blank_Name_Should_Return_400()
        {
            _service.Reset();
            var payload = new { name = "   " };
            var response = await _client.SendAsync(Authorized(HttpMethod.Put, "/api/teams/1", payload));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Team name cannot be empty", content);
        }

        [Fact]
        public async Task UpdateAsync_NonExisting_Should_Return_404()
        {
            _service.Reset();
            var payload = new { name = "Updated" };
            var response = await _client.SendAsync(Authorized(HttpMethod.Put, "/api/teams/999", payload));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeleteAsync_Should_Return_204_When_Deleted()
        {
            _service.Reset();
            var response = await _client.SendAsync(Authorized(HttpMethod.Delete, "/api/teams/1"));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task DeleteAsync_NonExisting_Should_Return_404()
        {
            _service.Reset();
            var response = await _client.SendAsync(Authorized(HttpMethod.Delete, "/api/teams/999"));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task AddMember_Should_Return_200_For_Existing_Team_And_User()
        {
            _service.Reset();
            var payload = new { userId = "u3" };
            var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/teams/1/members", payload, role: "TeamLead"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("u3", _service.GetMembers(1).Select(m => m.Id));
        }

        [Fact]
        public async Task AddMember_Should_Return_404_For_Unknown_Team_Or_User()
        {
            _service.Reset();
            var payload = new { userId = "missing" };
            var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/teams/999/members", payload, role: "TeamLead"));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RemoveMember_Should_Return_200_When_Removed()
        {
            _service.Reset();
            var payload = new { userId = "u1" };
            var response = await _client.SendAsync(Authorized(HttpMethod.Delete, "/api/teams/1/members", payload, role: "TeamLead"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(_service.GetMembers(1), m => m.Id == "u1");
        }

        [Fact]
        public async Task RemoveMember_Should_Return_404_For_Unknown_Team_Or_Member()
        {
            _service.Reset();
            var payload = new { userId = "missing" };
            var response = await _client.SendAsync(Authorized(HttpMethod.Delete, "/api/teams/1/members", payload, role: "TeamLead"));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetMembers_Should_Return_List_For_TeamLead()
        {
            _service.Reset();
            var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/teams/1/members", role: "TeamLead"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var members = await Deserialize<List<UserDto>>(response);
            Assert.NotNull(members);
            Assert.Contains(members!, m => m.Id == "u1");
        }

        [Fact]
        public async Task Members_Endpoints_Should_Return_403_For_User_Role()
        {
            _service.Reset();
            var payload = new { userId = "u2" };

            var add = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/teams/1/members", payload, role: "User"));
            Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);

            var remove = await _client.SendAsync(Authorized(HttpMethod.Delete, "/api/teams/1/members", payload, role: "User"));
            Assert.Equal(HttpStatusCode.Forbidden, remove.StatusCode);

            var list = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/teams/1/members", role: "User"));
            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        }

        private HttpRequestMessage Authorized(HttpMethod method, string url, object? body = null, string role = "User")
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.Add("X-Test-UserId", "user1");
            req.Headers.Add("X-Test-Role", role);
            req.Headers.Add("X-Test-TeamId", "1");
            req.Headers.Add("X-Test-SubscriptionId", "sub-1");

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

        private sealed class FakeTeamService : ITeamService
        {
            private readonly object _sync = new();
            private List<TeamRecord> _teams = new();
            private HashSet<string> _users = new();

            public FakeTeamService()
            {
                Reset();
            }

            public void Reset()
            {
                lock (_sync)
                {
                    _teams = new List<TeamRecord>
                    {
                        new TeamRecord(1, "Alpha", "First team", new[] { "u1", "u2" }),
                        new TeamRecord(2, "Beta", "Second team", new[] { "u3" })
                    };
                    _users = new HashSet<string>(new[] { "u1", "u2", "u3", "u4" });
                }
            }

            public IReadOnlyList<UserDto> GetMembers(int teamId)
            {
                lock (_sync)
                {
                    var team = _teams.FirstOrDefault(t => t.Id == teamId);
                    if (team is null) return Array.Empty<UserDto>();
                    return team.Members.Select(ToUserDto).ToList();
                }
            }

            public Task<IReadOnlyList<TeamDto>> GetAllAsync()
            {
                lock (_sync)
                {
                    return Task.FromResult<IReadOnlyList<TeamDto>>(_teams.Select(ToDto).ToList());
                }
            }

            public Task<TeamDto?> GetByIdAsync(int id)
            {
                lock (_sync)
                {
                    return Task.FromResult<TeamDto?>(_teams.Where(t => t.Id == id).Select(ToDto).FirstOrDefault());
                }
            }

            public Task<TeamDto> CreateAsync(CreateTeamDto dto)
            {
                lock (_sync)
                {
                    var nextId = _teams.Count == 0 ? 1 : _teams.Max(t => t.Id) + 1;
                    var record = new TeamRecord(nextId, dto.Name, dto.Description, Array.Empty<string>());
                    _teams.Add(record);
                    return Task.FromResult(ToDto(record));
                }
            }

            public Task<TeamDto?> UpdateAsync(int id, UpdateTeamDto dto)
            {
                lock (_sync)
                {
                    var team = _teams.FirstOrDefault(t => t.Id == id);
                    if (team is null) return Task.FromResult<TeamDto?>(null);

                    if (dto.Name is not null)
                        team.Name = dto.Name.Trim();
                    if (dto.Description is not null)
                        team.Description = dto.Description.Trim();

                    return Task.FromResult<TeamDto?>(ToDto(team));
                }
            }

            public Task<bool> DeleteAsync(int id)
            {
                lock (_sync)
                {
                    var team = _teams.FirstOrDefault(t => t.Id == id);
                    if (team is null) return Task.FromResult(false);
                    _teams.Remove(team);
                    return Task.FromResult(true);
                }
            }

            public Task<bool> AddMemberAsync(int teamId, string userId)
            {
                lock (_sync)
                {
                    var team = _teams.FirstOrDefault(t => t.Id == teamId);
                    if (team is null || !_users.Contains(userId)) return Task.FromResult(false);
                    team.Members.Add(userId);
                    return Task.FromResult(true);
                }
            }

            public Task<bool> RemoveMemberAsync(int teamId, string userId)
            {
                lock (_sync)
                {
                    var team = _teams.FirstOrDefault(t => t.Id == teamId);
                    if (team is null || !team.Members.Remove(userId)) return Task.FromResult(false);
                    return Task.FromResult(true);
                }
            }

            public Task<IReadOnlyList<UserDto>> GetMembersAsync(int teamId)
            {
                lock (_sync)
                {
                    var team = _teams.FirstOrDefault(t => t.Id == teamId);
                    if (team is null) return Task.FromResult<IReadOnlyList<UserDto>>(Array.Empty<UserDto>());
                    return Task.FromResult<IReadOnlyList<UserDto>>(team.Members.Select(ToUserDto).ToList());
                }
            }

            private static TeamDto ToDto(TeamRecord t) => new()
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                MemberCount = t.Members.Count
            };

            private static UserDto ToUserDto(string userId) => new()
            {
                Id = userId,
                Email = $"{userId}@test.local",
                DisplayName = userId.ToUpperInvariant()
            };

            private sealed class TeamRecord
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public string? Description { get; set; }
                public HashSet<string> Members { get; set; }

                public TeamRecord(int id, string name, string? description, IEnumerable<string> members)
                {
                    Id = id;
                    Name = name;
                    Description = description;
                    Members = new HashSet<string>(members);
                }
            }
        }
    }
}
