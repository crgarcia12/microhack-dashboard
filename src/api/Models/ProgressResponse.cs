namespace Api.Models;

public class ProgressResponse
{
    public string TeamId { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public int TotalChallenges { get; set; }
    public int CompletedChallenges { get; set; }
    public bool Completed { get; set; }
}
