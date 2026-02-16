// Derived from: chat-interface.feature, conversation-engine.feature
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using Api.Models;

namespace Api.Tests.Integration;

public class ChatEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ChatEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // Derived from: conversation-engine.feature — AC-CE-002
    [Fact]
    public async Task Should_ReturnResponse_When_PostingValidMessage()
    {
        // Arrange
        var request = new ChatRequest { Message = "Show me running shoes" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert — will fail until POST /api/chat is implemented
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        contentType.Should().NotBeNull();
    }

    // Derived from: conversation-engine.feature — AC-CE-014
    [Fact]
    public async Task Should_Return400_When_MessageIsEmpty()
    {
        // Arrange
        var request = new ChatRequest { Message = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert — will fail until POST /api/chat with validation is implemented
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Message is required");
    }

    // Derived from: conversation-engine.feature — Whitespace-only message returns 400
    [Fact]
    public async Task Should_Return400_When_MessageIsWhitespace()
    {
        // Arrange
        var request = new ChatRequest { Message = "   " };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert — will fail until POST /api/chat with validation is implemented
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Message is required");
    }

    // Derived from: conversation-engine.feature — AC-CE-006
    [Fact]
    public async Task Should_MaintainContext_When_SessionIdIsProvided()
    {
        // Arrange — first message to create a session
        var firstRequest = new ChatRequest { Message = "Show me hiking boots" };
        var firstResponse = await _client.PostAsJsonAsync("/api/chat", firstRequest);

        // Will fail until /api/chat is implemented
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var firstResult = JsonSerializer.Deserialize<ChatResponse>(firstBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        firstResult.Should().NotBeNull();
        var sessionId = firstResult!.SessionId;
        sessionId.Should().NotBeNullOrEmpty();

        // Act — second message using same session
        var secondRequest = new ChatRequest { Message = "Is the first one in stock?", SessionId = sessionId };
        var secondResponse = await _client.PostAsJsonAsync("/api/chat", secondRequest);

        // Assert — context should be maintained
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Derived from: conversation-engine.feature — AC-CE-001
    [Fact]
    public async Task Should_CreateNewSession_When_NoSessionIdProvided()
    {
        // Arrange
        var request = new ChatRequest { Message = "Hello" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert — will fail until /api/chat is implemented
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChatResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result.Should().NotBeNull();
        result!.SessionId.Should().NotBeNullOrEmpty();
    }

    // Derived from: conversation-engine.feature — AC-CE-002
    [Fact]
    public async Task Should_IncludeSessionId_InResponse()
    {
        // Arrange
        var request = new ChatRequest { Message = "What products do you have?" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert — will fail until /api/chat is implemented
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChatResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result.Should().NotBeNull();
        result!.SessionId.Should().NotBeNullOrEmpty();
    }

    // Derived from: conversation-engine.feature — AC-CE-002 Response includes message content
    [Fact]
    public async Task Should_ReturnJsonResponse_When_ResponseIsGenerated()
    {
        // Arrange
        var request = new ChatRequest { Message = "Show me wireless headphones" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        contentType.Should().Be("application/json");
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChatResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result.Should().NotBeNull();
        result!.Message.Should().NotBeNullOrEmpty();
    }
}
