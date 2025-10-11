namespace TaskManager.Api.Dtos
{
    public sealed class TeamDto
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? Description { get; init; }

        public int MemberCount { get; init; }
    }
}
