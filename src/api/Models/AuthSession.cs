namespace Api.Models;

public class AuthSession
{
    public string SessionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Team { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
