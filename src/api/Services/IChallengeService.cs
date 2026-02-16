using Api.Models;

namespace Api.Services;

public interface IChallengeService
{
    int TotalChallenges { get; }
    IReadOnlyList<Challenge> GetChallenges();
    Challenge? GetChallenge(int number);
    ProgressResponse GetTeamProgress(string teamId);
    (ProgressResponse? Progress, string? Error) Approve(string teamId);
    (ProgressResponse? Progress, string? Error) Revert(string teamId);
    (ProgressResponse? Progress, string? Error) Reset(string teamId);
}
