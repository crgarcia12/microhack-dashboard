using Api.Models;

namespace Api.Services;

public class PlaceholderConversationHandler : IConversationHandler
{
    public Task<string> HandleAsync(string message, List<ChatMessage> history)
    {
        return Task.FromResult("This is a shell template. Implement your conversation logic here.");
    }
}
