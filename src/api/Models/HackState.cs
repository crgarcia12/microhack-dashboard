using System.Text.Json.Serialization;

namespace Api.Models;

public class HackState
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "not_started"; // not_started, configuration, waiting, active, completed
    
    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }
    
    [JsonPropertyName("configuredBy")]
    public string? ConfiguredBy { get; set; }
    
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
