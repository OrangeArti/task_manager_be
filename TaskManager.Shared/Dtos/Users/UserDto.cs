using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.Dtos.Users;

public class UserDto
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [EmailAddress]
    [Required]
    public string Email { get; set; } = string.Empty;

    [StringLength(200)]
    public string? DisplayName { get; set; }

    [Url]
    public string? AvatarUrl { get; set; }

    public int? TeamId { get; set; }

    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}
