namespace TaskManager.Api.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; } = false;

        public ApplicationUser? User { get; set; }
    }
}