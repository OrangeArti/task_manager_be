namespace TaskManager.Api.Dtos.Groups;

public record GroupDto(int Id, string Name, int OrganizationId, string? Description, DateTime CreatedAt);
