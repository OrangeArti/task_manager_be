namespace TaskManager.Api.Dtos
{
    public class RoleAssignRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }
}
