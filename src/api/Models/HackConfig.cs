using System.Text.Json.Serialization;

namespace Api.Models;

public class HackConfig
{
    [JsonPropertyName("contentPath")]
    public string? ContentPath { get; set; }
    
    [JsonPropertyName("teams")]
    public List<TeamConfig> Teams { get; set; } = new();
    
    [JsonPropertyName("coaches")]
    public List<string> Coaches { get; set; } = new();
    
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class TeamConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("members")]
    public List<string> Members { get; set; } = new();
}
