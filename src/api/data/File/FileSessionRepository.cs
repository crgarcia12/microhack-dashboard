using System.Collections.Concurrent;
using Api.Models;

namespace Api.Data.File;

public class FileSessionRepository : ISessionRepository
{
    private readonly ConcurrentDictionary<string, AuthSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _userSessionMap = new(); // username -> sessionId

    public AuthSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public void SaveSession(AuthSession session)
    {
        _sessions[session.SessionId] = session;
        _userSessionMap[session.Username.ToLowerInvariant()] = session.SessionId;
    }

    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            _userSessionMap.TryRemove(session.Username.ToLowerInvariant(), out _);
        }
    }

    public void RemoveSessionsByUsername(string username)
    {
        if (_userSessionMap.TryRemove(username.ToLowerInvariant(), out var oldSessionId))
        {
            _sessions.TryRemove(oldSessionId, out _);
        }
    }
}
