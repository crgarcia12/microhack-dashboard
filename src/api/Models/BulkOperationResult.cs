using System.Text.Json.Serialization;

namespace Api.Models;

public class BulkOperationResult
{
    public string TeamName { get; set; } = string.Empty;
    public bool Success { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}
