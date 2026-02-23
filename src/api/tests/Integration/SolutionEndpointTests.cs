using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests.Integration;

[Trait("Category", "Integration")]
public class SolutionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SolutionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> LoginAs(string username, string password)
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

    private async Task SetMode(string mode, bool? participantSolutionsVisible = null)
    {
        var techlead = await LoginAs("techlead", "pass123");
        var payload = new
        {
            mode,
            participantSolutionsVisible,
            contentPath = "hackcontent",
            teams = Array.Empty<object>(),
            coaches = Array.Empty<string>()
        };

        var response = await techlead.PostAsJsonAsync("/api/hack/config", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSolutions_AsParticipant_TeamModeDefault_Returns403()
    {
        await SetMode("team", null);

        var participant = await LoginAs("hacker1", "pass123");
        var response = await participant.GetAsync("/api/solutions");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSolutions_AsParticipant_TeamModeWithVisibilityEnabled_Returns200()
    {
        await SetMode("team", true);

        var participant = await LoginAs("hacker1", "pass123");
        var response = await participant.GetAsync("/api/solutions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSolutions_AsParticipant_IndividualModeDefault_Returns200()
    {
        await SetMode("individual", null);

        var participant = await LoginAs("hacker1", "pass123");
        var response = await participant.GetAsync("/api/solutions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
