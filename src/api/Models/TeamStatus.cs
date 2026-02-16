namespace Api.Models;

public class TeamStatus
{
    public string TeamName { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public int TotalChallenges { get; set; }
    public bool IsCompleted { get; set; }
    public string ManualTimerStatus { get; set; } = "stopped";
    public int ElapsedSeconds { get; set; }
    public Dictionary<string, int> ChallengeTimes { get; set; } = new();
}
