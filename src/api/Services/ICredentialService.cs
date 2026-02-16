using Api.Models;

namespace Api.Services;

public interface ICredentialService
{
    TeamCredentials GetCredentials(string teamName);
}
