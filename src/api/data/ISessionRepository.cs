using Api.Models;

namespace Api.Data;

public interface ISessionRepository
{
    AuthSession? GetSession(string sessionId);
    void SaveSession(AuthSession session);
    void RemoveSession(string sessionId);
    void RemoveSessionsByUsername(string username);
}
