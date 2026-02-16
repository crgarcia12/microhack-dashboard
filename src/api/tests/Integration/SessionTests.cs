// Derived from: conversation-engine.feature, chat-interface.feature
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;

namespace Api.Tests.Integration;

public class SessionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SessionTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // Derived from: conversation-engine.feature — AC-CE-001
    [Fact]
    public async Task Should_CreateNewSession_When_PostToSessions()
    {
        // Act
        var response = await _client.PostAsync("/api/sessions", null);

        // Assert — will fail until POST /api/sessions is implemented
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body);
        var sessionId = result.GetProperty("sessionId").GetString();
        sessionId.Should().NotBeNullOrEmpty();
        Guid.TryParse(sessionId, out _).Should().BeTrue("session ID should be a valid UUID");
    }

    // Derived from: conversation-engine.feature — AC-CE-006
    [Fact]
    public async Task Should_MaintainConversationHistory_AcrossMultipleMessages()
    {
        // Arrange — create session
        var createResponse = await _client.PostAsync("/api/sessions", null);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var sessionId = JsonSerializer.Deserialize<JsonElement>(createBody)
            .GetProperty("sessionId").GetString();

        // Act — send multiple messages
        var msg1 = new { Message = "Show me hiking boots", SessionId = sessionId };
        var response1 = await _client.PostAsJsonAsync("/api/chat", msg1);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        var msg2 = new { Message = "Is the first one in stock?", SessionId = sessionId };
        var response2 = await _client.PostAsJsonAsync("/api/chat", msg2);

        // Assert — will fail until session context is implemented
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Derived from: conversation-engine.feature — AC-CE-003
    [Fact]
    public async Task Should_ClearSession_When_DeleteIsCalled()
    {
        // Arrange — create session
        var createResponse = await _client.PostAsync("/api/sessions", null);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var sessionId = JsonSerializer.Deserialize<JsonElement>(createBody)
            .GetProperty("sessionId").GetString();

        // Act — delete session
        var deleteResponse = await _client.DeleteAsync($"/api/sessions/{sessionId}");

        // Assert — will fail until DELETE /api/sessions/{id} is implemented
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify subsequent requests return 404
        var chatRequest = new { Message = "Hello", SessionId = sessionId };
        var chatResponse = await _client.PostAsJsonAsync("/api/chat", chatRequest);
        chatResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Derived from: conversation-engine.feature — AC-CE-004
    [Fact]
    public async Task Should_HandleGracefully_When_SessionReaches50TurnLimit()
    {
        // Arrange — create session
        var createResponse = await _client.PostAsync("/api/sessions", null);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var sessionId = JsonSerializer.Deserialize<JsonElement>(createBody)
            .GetProperty("sessionId").GetString();

        // Act — send 51 messages (user + assistant = 2 messages per exchange, so 25 exchanges = 50 messages)
        // The 26th user message (51st total message) should be rejected
        // Note: This test will fail until session management with message limits is implemented
        HttpResponseMessage? lastResponse = null;
        for (int i = 0; i < 26; i++)
        {
            var msg = new { Message = $"Message {i + 1}", SessionId = sessionId };
            lastResponse = await _client.PostAsJsonAsync("/api/chat", msg);

            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
                break;
        }

        // Assert — the 51st message should be rejected with 429
        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await lastResponse.Content.ReadAsStringAsync();
        body.Should().Contain("maximum length");
    }

    // Derived from: conversation-engine.feature — Session deletion for non-existent session returns 404
    [Fact]
    public async Task Should_Return404_When_DeletingNonExistentSession()
    {
        // Arrange — first verify the endpoint exists by creating then deleting a session
        var createResponse = await _client.PostAsync("/api/sessions", null);
        // Will fail until POST /api/sessions is implemented
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "session creation endpoint must exist before testing deletion of non-existent session");

        // Act
        var response = await _client.DeleteAsync("/api/sessions/999e8400-e29b-41d4-a716-446655440000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Session not found");
    }
}
