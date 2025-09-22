namespace TaskManager.Api.Dtos
{
    public class UserSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public bool EmailConfirmed { get; set; }
    }
}