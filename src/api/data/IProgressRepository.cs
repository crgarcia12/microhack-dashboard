using Api.Models;

namespace Api.Data;

public interface IProgressRepository
{
    TeamProgress? GetProgress(string teamId);
    IReadOnlyDictionary<string, TeamProgress> GetAllProgress();
    void SaveProgress(TeamProgress progress);
}
