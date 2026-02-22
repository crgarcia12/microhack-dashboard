using System.Text.Json.Serialization;

namespace Api.Models;

public class DataStoreInfo
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;
}
