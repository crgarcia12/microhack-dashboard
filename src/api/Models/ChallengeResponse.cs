namespace Api.Models;

public class ChallengeListItem
{
    public int ChallengeNumber { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ChallengeDetail
{
    public int ChallengeNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentHtml { get; set; } = string.Empty;
}
