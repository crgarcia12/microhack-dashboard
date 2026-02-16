namespace Api.Models;

public class Challenge
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RawMarkdown { get; set; } = string.Empty;
}
