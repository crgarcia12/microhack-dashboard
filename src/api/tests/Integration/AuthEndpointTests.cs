using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration;

[Trait("Category", "Integration")]
public class AuthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> LoginAndGetClient(string username, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var session = cookies.First(c => c.StartsWith("hackbox_session="));
            client.DefaultRequestHeaders.Add("Cookie", session.Split(';')[0]);
        }
        return client;
    }

    // POST /api/auth/login

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndSetsCookie()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "hacker1", password = "pass123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Set-Cookie");
        response.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("hackbox_session="));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "hacker1", password = "wrong" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "nonexistent", password = "pass123" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithMissingFields_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "hacker1" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithEmptyBody_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("hacker1")]
    [InlineData("Hacker1")]
    [InlineData("HACKER1")]
    public async Task Login_IsCaseInsensitiveForUsername(string username)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "pass123" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_ReturnsUserIdentityInResponseBody()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "hacker1", password = "pass123" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("username").GetString().Should().Be("hacker1");
        body.GetProperty("role").GetString().Should().Be("participant");
        body.GetProperty("team").GetString().Should().Be("team-alpha");
    }

    [Fact]
    public async Task Login_SessionCookieIsHttpOnly()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "hacker1", password = "pass123" });

        var setCookie = response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("hackbox_session="));
        setCookie.ToLowerInvariant().Should().Contain("httponly");
    }

    [Fact]
    public async Task Login_SecondLogin_InvalidatesPreviousSession()
    {
        var client1 = await LoginAndGetClient("hacker1", "pass123");
        // Second login as same user creates a new session, invalidating the first
        var client2 = await LoginAndGetClient("hacker1", "pass123");

        var meWithOldSession = await client1.GetAsync("/api/auth/me");
        meWithOldSession.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var meWithNewSession = await client2.GetAsync("/api/auth/me");
        meWithNewSession.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // POST /api/auth/logout

    [Fact]
    public async Task Logout_WithValidSession_Returns200AndClearsCookie()
    {
        var client = await LoginAndGetClient("hacker1", "pass123");
        var response = await client.PostAsync("/api/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().Be("Logged out");
    }

    [Fact]
    public async Task Logout_WithoutSession_Returns200()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/auth/logout", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_InvalidatesSessionOnServer()
    {
        var client = await LoginAndGetClient("hacker1", "pass123");
        await client.PostAsync("/api/auth/logout", null);

        // Session cookie is still in the header but server-side session is removed
        var meResponse = await client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /api/auth/me

    [Fact]
    public async Task Me_WithValidSession_ReturnsUserIdentity()
    {
        var client = await LoginAndGetClient("hacker1", "pass123");
        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("username").GetString().Should().Be("hacker1");
        body.GetProperty("role").GetString().Should().Be("participant");
        body.GetProperty("team").GetString().Should().Be("team-alpha");
    }

    [Fact]
    public async Task Me_WithoutSession_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithInvalidSessionId_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "hackbox_session=invalid_session_id");
        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_TechleadUser_HasNullTeamId()
    {
        var client = await LoginAndGetClient("techlead", "pass123");
        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("team").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
