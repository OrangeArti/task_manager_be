using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using TaskManager.Shared.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection("Gateway"));
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

var apiGroup = app.MapGroup("/api");

apiGroup.MapGet("/health", GetAggregatedHealth)
    .WithName("GetAggregatedHealth")
    .WithTags("Health")
    .WithOpenApi();

MapServiceProxy(apiGroup, "tasks", "Tasks");
MapServiceProxy(apiGroup, "teams", "Teams");
MapServiceProxy(apiGroup, "users", "Users");
MapServiceProxy(apiGroup, "auth", "Auth");

app.Run();

static void MapServiceProxy(RouteGroupBuilder apiGroup, string serviceKey, string tag)
{
    var methods = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" };

    apiGroup.MapMethods($"/{serviceKey}", methods,
            (HttpContext context,
                IHttpClientFactory factory,
                IOptions<GatewayOptions> options,
                CancellationToken cancellationToken) =>
                ProxyAsync(context, factory, options, serviceKey, string.Empty, cancellationToken))
        .WithTags(tag)
        .ExcludeFromDescription();

    apiGroup.MapMethods($"/{serviceKey}/{{*path}}", methods,
            (HttpContext context,
                IHttpClientFactory factory,
                IOptions<GatewayOptions> options,
                string path,
                CancellationToken cancellationToken) =>
                ProxyAsync(context, factory, options, serviceKey, path, cancellationToken))
        .WithTags(tag)
        .ExcludeFromDescription();
}

static async Task ProxyAsync(
    HttpContext context,
    IHttpClientFactory clientFactory,
    IOptions<GatewayOptions> options,
    string serviceKey,
    string? path,
    CancellationToken cancellationToken)
{
    if (!options.Value.Services.TryGetValue(serviceKey, out var service))
    {
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        await context.Response.WriteAsync($"Service '{serviceKey}' is not configured.", cancellationToken);
        return;
    }

    var targetPath = CombineUri(service.ForwardPath, path);
    var targetUri = BuildTargetUri(service.BaseUrl, targetPath, context.Request.QueryString);
    using var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

    var contentHeaders = new List<KeyValuePair<string, StringValues>>();
    CopyRequestHeaders(context.Request.Headers, requestMessage, contentHeaders);

    if (RequestHasBody(context.Request))
    {
        context.Request.EnableBuffering();
        var stream = new MemoryStream();
        await context.Request.Body.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;
        context.Request.Body.Position = 0;
        requestMessage.Content = new StreamContent(stream);

        foreach (var header in contentHeaders)
        {
            requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Host", context.Request.Host.Value);
    requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);
    if (context.Connection.RemoteIpAddress is { } remoteIp)
    {
        requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-For", remoteIp.ToString());
    }

    using var response = await clientFactory.CreateClient()
        .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    context.Response.StatusCode = (int)response.StatusCode;
    CopyResponseHeaders(context.Response, response);

    await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
}

static async Task<IResult> GetAggregatedHealth(
    IOptions<GatewayOptions> options,
    IHttpClientFactory clientFactory,
    CancellationToken cancellationToken)
{
    var snapshots = new List<HealthStatusDto>();

    foreach (var (key, service) in options.Value.Services)
    {
        var targetUri = CombineUri(service.BaseUrl, service.HealthPath);
        try
        {
            using var response = await clientFactory.CreateClient().GetAsync(targetUri, cancellationToken);
            HealthStatusDto? payload = null;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<HealthStatusDto>(cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                // downstream returned a non-standard payload, fallback below
            }

            var serviceName = !string.IsNullOrWhiteSpace(service.Name) ? service.Name : key;

            if (payload is null)
            {
                payload = new HealthStatusDto
                {
                    Service = serviceName,
                    Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                    Details = $"HTTP {(int)response.StatusCode}",
                    CheckedAtUtc = DateTime.UtcNow
                };
            }
            else
            {
                payload.Service ??= serviceName;
                payload.CheckedAtUtc = payload.CheckedAtUtc == default ? DateTime.UtcNow : payload.CheckedAtUtc;
            }

            snapshots.Add(payload);
        }
        catch (Exception ex)
        {
            snapshots.Add(new HealthStatusDto
            {
                Service = !string.IsNullOrWhiteSpace(service.Name) ? service.Name : key,
                Status = "Unhealthy",
                Details = ex.Message,
                CheckedAtUtc = DateTime.UtcNow
            });
        }
    }

    return Results.Ok(new AggregatedHealthResponse
    {
        CheckedAtUtc = DateTime.UtcNow,
        Services = snapshots
    });
}

static Uri BuildTargetUri(string baseUrl, string? path, QueryString query)
{
    var builder = new StringBuilder();
    builder.Append(baseUrl.TrimEnd('/'));

    if (!string.IsNullOrEmpty(path))
    {
        builder.Append('/');
        builder.Append(path.TrimStart('/'));
    }

    if (query.HasValue)
    {
        builder.Append(query.Value);
    }

    return new Uri(builder.ToString(), UriKind.Absolute);
}

static string CombineUri(string baseUrl, string? path)
{
    if (string.IsNullOrEmpty(path))
    {
        return baseUrl;
    }

    return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}

static void CopyRequestHeaders(
    IHeaderDictionary source,
    HttpRequestMessage destination,
    List<KeyValuePair<string, StringValues>> contentHeaders)
{
    foreach (var header in source)
    {
        if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
        {
            contentHeaders.Add(new KeyValuePair<string, StringValues>(header.Key, header.Value));
            continue;
        }

        destination.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }
}

static void CopyResponseHeaders(HttpResponse response, HttpResponseMessage source)
{
    foreach (var header in source.Headers)
    {
        response.Headers[header.Key] = header.Value.ToArray();
    }

    foreach (var header in source.Content.Headers)
    {
        response.Headers[header.Key] = header.Value.ToArray();
    }

    response.Headers.Remove("transfer-encoding");
    response.Headers.Remove("connection");
}

static bool RequestHasBody(HttpRequest request)
{
    if (request.ContentLength.HasValue && request.ContentLength.Value > 0)
    {
        return true;
    }

    if (request.Headers.TryGetValue("Transfer-Encoding", out var transferEncoding))
    {
        return transferEncoding.Count > 0 &&
               !string.Equals(transferEncoding.ToString(), "identity", StringComparison.OrdinalIgnoreCase);
    }

    return false;
}

internal sealed class GatewayOptions
{
    public GatewayCorsOptions Cors { get; set; } = new();

    public IDictionary<string, GatewayServiceOptions> Services { get; set; }
        = new Dictionary<string, GatewayServiceOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["tasks"] = new() { Name = "tasks-service", BaseUrl = "http://tasks-service:8080", HealthPath = "/health" },
            ["teams"] = new() { Name = "teams-service", BaseUrl = "http://teams-service:8080", HealthPath = "/health" },
            ["users"] = new() { Name = "users-service", BaseUrl = "http://users-service:8080", HealthPath = "/health" }
        };
}

internal sealed class GatewayCorsOptions
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

internal sealed class GatewayServiceOptions
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ForwardPath { get; set; } = string.Empty;
    public string HealthPath { get; set; } = "/health";
}

internal sealed class AggregatedHealthResponse
{
    public DateTime CheckedAtUtc { get; set; }
    public IReadOnlyCollection<HealthStatusDto> Services { get; set; } = Array.Empty<HealthStatusDto>();
}
