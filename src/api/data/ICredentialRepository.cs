using Api.Models;

namespace Api.Data;

public interface ICredentialRepository
{
    TeamCredentials GetCredentials(string teamName);
    List<TeamCredentials> GetAllCredentials();
    void SaveCredentials(TeamCredentials credentials);
    void DeleteCredentials(string teamName);
    bool HasCredentials();
    void SeedCredentials(List<TeamCredentials> teams);
}
