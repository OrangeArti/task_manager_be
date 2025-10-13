namespace TaskManager.Shared.Security;

/// <summary>
/// Shared configuration contract for JWT token generation and validation.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "TaskManagerApi";

    public string Audience { get; init; } = "TaskManagerApi";

    public string Key { get; init; } = "SuperSecretKey123!";
}

