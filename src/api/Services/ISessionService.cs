using Api.Models;

namespace Api.Services;

public interface ISessionService
{
    Task<string> CreateSessionAsync();
    Task<bool> SessionExistsAsync(string sessionId);
    Task<bool> DeleteSessionAsync(string sessionId);
    Task<int> GetMessageCountAsync(string sessionId);
    Task AddMessageAsync(string sessionId, ChatMessage message);
    Task<List<ChatMessage>> GetMessagesAsync(string sessionId);
}
