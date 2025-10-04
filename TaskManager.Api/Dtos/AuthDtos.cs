namespace TaskManager.Api.Dtos
{
    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
    }
}