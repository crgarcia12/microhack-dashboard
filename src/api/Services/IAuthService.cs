using Api.Models;

namespace Api.Services;

public interface IAuthService
{
    User? ValidateCredentials(string username, string password);
    AuthSession CreateSession(User user);
    AuthSession? GetSession(string sessionId);
    void RemoveSession(string sessionId);
    IReadOnlyList<string> GetAllTeams();
}
