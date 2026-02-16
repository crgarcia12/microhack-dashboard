using System.Text.Json.Serialization;

namespace Api.Models;

public class UserConfig
{
    [JsonPropertyName("users")]
    public List<User> Users { get; set; } = new();
}
