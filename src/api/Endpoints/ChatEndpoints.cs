using Api.Models;
using Api.Services;

namespace Api.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        app.MapPost("/api/chat", HandleChat);
        app.MapPost("/api/sessions", CreateSession);
        app.MapDelete("/api/sessions/{sessionId}", DeleteSession);
    }

    private static async Task HandleChat(
        HttpContext context,
        ISessionService sessionService,
        IConversationHandler conversationHandler)
    {
        var request = await context.Request.ReadFromJsonAsync<ChatRequest>();

        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Message is required" });
            return;
        }

        string sessionId;
        if (!string.IsNullOrEmpty(request.SessionId))
        {
            if (!await sessionService.SessionExistsAsync(request.SessionId))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsJsonAsync(new { error = "Session not found" });
                return;
            }
            sessionId = request.SessionId;
        }
        else
        {
            sessionId = await sessionService.CreateSessionAsync();
        }

        // Check 50-turn limit (user + assistant messages)
        var messageCount = await sessionService.GetMessageCountAsync(sessionId);
        if (messageCount >= 50)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsJsonAsync(new { error = "Conversation has reached the maximum length of 50 messages" });
            return;
        }

        // Add user message
        await sessionService.AddMessageAsync(sessionId, new ChatMessage
        {
            Role = "user",
            Content = request.Message
        });

        // Get conversation history and generate response
        var history = await sessionService.GetMessagesAsync(sessionId);
        string responseMessage;
        try
        {
            // Small delay to allow UI to show typing indicator
            await Task.Delay(200);
            responseMessage = await conversationHandler.HandleAsync(request.Message, history);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            return;
        }

        // Add assistant message
        await sessionService.AddMessageAsync(sessionId, new ChatMessage
        {
            Role = "assistant",
            Content = responseMessage
        });

        var chatResponse = new ChatResponse
        {
            SessionId = sessionId,
            Message = responseMessage
        };

        await context.Response.WriteAsJsonAsync(chatResponse);
    }

    private static async Task<IResult> CreateSession(ISessionService sessionService)
    {
        var sessionId = await sessionService.CreateSessionAsync();
        return Results.Created($"/api/sessions/{sessionId}", new { sessionId });
    }

    private static async Task<IResult> DeleteSession(string sessionId, ISessionService sessionService)
    {
        var deleted = await sessionService.DeleteSessionAsync(sessionId);
        if (!deleted)
        {
            return Results.NotFound(new { error = "Session not found" });
        }
        return Results.NoContent();
    }
}
