using Api.Models;

namespace Api.Services;

public interface IConversationHandler
{
    Task<string> HandleAsync(string message, List<ChatMessage> history);
}
