namespace Api.Models;

public class TimerState
{
    public string TeamName { get; set; } = string.Empty;
    public ManualTimerState ManualTimer { get; set; } = new();
    public DateTime? TimerStartedAt { get; set; }
    public Dictionary<string, int> ChallengeTimes { get; set; } = new();
}
