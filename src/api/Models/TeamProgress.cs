namespace Api.Models;

public class TeamProgress
{
    public string TeamId { get; set; } = string.Empty;
    public int CurrentStep { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
