using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace TaskManager.Tests
{
    public class HealthControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public HealthControllerTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Health_Should_Return_200_And_Healthy_Status()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert status code
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert JSON payload
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // status == "Healthy"
            var status = root.GetProperty("status").GetString();
            Assert.Equal("Healthy", status);

            // service == "tasks-core"
            var service = root.GetProperty("service").GetString();
            Assert.Equal("tasks-core", service);

            // наличие CheckedAtUtc (типа DateTime, строкой)
            Assert.True(root.TryGetProperty("checkedAtUtc", out var checkedAtUtc));
            Assert.Equal(JsonValueKind.String, checkedAtUtc.ValueKind);
        }
    }
}