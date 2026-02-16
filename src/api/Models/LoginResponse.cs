using System.Text.Json.Serialization;

namespace Api.Models;

public class LoginResponse
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    public string? Team { get; set; }
}
