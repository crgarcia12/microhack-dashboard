namespace Api.Models;

public class ManualTimerState
{
    public string Status { get; set; } = "stopped";
    public DateTime? StartedAt { get; set; }
    public int AccumulatedSeconds { get; set; }
}
