namespace Api.Models;

public class LabConfig
{
    public const string SectionName = "LabEnvironment";

    public bool Enabled { get; set; }
    public List<LabEndpoint> Endpoints { get; set; } = [];
}
