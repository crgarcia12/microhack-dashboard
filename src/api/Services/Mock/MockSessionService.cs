using System.Collections.Concurrent;
using Api.Models;

namespace Api.Services.Mock;

public class MockSessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public Task<string> CreateSessionAsync()
    {
        var session = new Session { Id = Guid.NewGuid().ToString() };
        _sessions[session.Id] = session;
        return Task.FromResult(session.Id);
    }

    public Task<bool> SessionExistsAsync(string sessionId) =>
        Task.FromResult(_sessions.ContainsKey(sessionId));

    public Task<bool> DeleteSessionAsync(string sessionId) =>
        Task.FromResult(_sessions.TryRemove(sessionId, out _));

    public Task<int> GetMessageCountAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            return Task.FromResult(session.Messages.Count);
        return Task.FromResult(0);
    }

    public Task AddMessageAsync(string sessionId, ChatMessage message)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            session.Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<List<ChatMessage>> GetMessagesAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            return Task.FromResult(new List<ChatMessage>(session.Messages));
        return Task.FromResult(new List<ChatMessage>());
    }
}
