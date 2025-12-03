using System.Collections.Generic;

namespace TaskManager.Api.Dtos
{
    /// <summary>
    /// Lightweight user info for public directory views.
    /// </summary>
    public sealed class PublicUserDto
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int? TeamId { get; init; }
        public List<string> Roles { get; init; } = new();
    }
}
