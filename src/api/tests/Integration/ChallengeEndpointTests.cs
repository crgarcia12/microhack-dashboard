using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration;

[Trait("Category", "Integration")]
public class ChallengeEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _tempDir;
    private readonly string _challengesDir;
    private readonly string _progressDir;

    public ChallengeEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"challenge_int_tests_{Guid.NewGuid():N}");
        _challengesDir = Path.Combine(_tempDir, "challenges");
        _progressDir = Path.Combine(_tempDir, "progress");
        Directory.CreateDirectory(_challengesDir);
        Directory.CreateDirectory(_progressDir);

        // Create test challenge files
        File.WriteAllText(Path.Combine(_challengesDir, "challenge-001.md"), "# First Challenge\nContent 1");
        File.WriteAllText(Path.Combine(_challengesDir, "challenge-002.md"), "# Second Challenge\nContent 2");
        File.WriteAllText(Path.Combine(_challengesDir, "challenge-003.md"), "# Third Challenge\nContent 3");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IChallengeService));
                if (descriptor != null) services.Remove(descriptor);

                services.AddSingleton<IChallengeService>(sp =>
                {
                    var repo = new Api.Data.File.FileProgressRepository(_progressDir,
                        NullLogger<Api.Data.File.FileProgressRepository>.Instance);
                    return new ChallengeService(_challengesDir, repo,
                        NullLogger<ChallengeService>.Instance);
                });
            });
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
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

    // GET /api/challenges

    [Fact]
    public async Task GetChallenges_Authenticated_Returns200WithChallengeList()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/challenges");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task GetChallenges_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/challenges");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetChallenges_LockedChallenges_HaveNullTitle()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/challenges");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // At step 1: challenge 1 = current, 2 = locked, 3 = locked
        var challenge3 = body.EnumerateArray().First(c => c.GetProperty("challengeNumber").GetInt32() == 3);
        challenge3.GetProperty("title").ValueKind.Should().Be(JsonValueKind.Null);
        challenge3.GetProperty("status").GetString().Should().Be("locked");
    }

    [Fact]
    public async Task GetChallenges_ReturnsCorrectStatusPerChallenge()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/challenges");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var items = body.EnumerateArray().ToList();
        items.First(c => c.GetProperty("challengeNumber").GetInt32() == 1)
            .GetProperty("status").GetString().Should().Be("current");
        items.First(c => c.GetProperty("challengeNumber").GetInt32() == 2)
            .GetProperty("status").GetString().Should().Be("locked");
    }

    // GET /api/challenges/{number}

    [Fact]
    public async Task GetChallenge_CurrentChallenge_Returns200WithContent()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/challenges/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("challengeNumber").GetInt32().Should().Be(1);
        body.GetProperty("title").GetString().Should().Be("First Challenge");
    }

    [Fact]
    public async Task GetChallenge_CompletedChallenge_Returns200()
    {
        // Advance past challenge 1 (as coach)
        var coach = await LoginAs("coach1", "pass123");
        await coach.PostAsync("/api/teams/progress/approve", null);

        // Now challenge 1 is completed — should still be accessible
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/challenges/1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetChallenge_LockedChallenge_Returns403()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/challenges/3");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetChallenge_NonExistent_Returns404()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/challenges/999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetChallenge_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/challenges/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /api/teams/progress

    [Fact]
    public async Task GetProgress_Authenticated_Returns200WithTeamProgress()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/teams/progress");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currentStep").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("totalChallenges").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task GetProgress_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/teams/progress");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProgress_AllCompleted_ReturnsCompletedTrue()
    {
        var coach = await LoginAs("coach1", "pass123");
        // Approve all 3 challenges
        await coach.PostAsync("/api/teams/progress/approve", null);
        await coach.PostAsync("/api/teams/progress/approve", null);
        await coach.PostAsync("/api/teams/progress/approve", null);

        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/teams/progress");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("completed").GetBoolean().Should().BeTrue();
    }

    // POST /api/teams/progress/approve

    [Fact]
    public async Task Approve_AsCoach_AdvancesToNextChallenge()
    {
        var coach = await LoginAs("coach1", "pass123");
        var response = await coach.PostAsync("/api/teams/progress/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currentStep").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Approve_AsParticipant_Returns403()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.PostAsync("/api/teams/progress/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/teams/progress/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Approve_AllCompleted_Returns409()
    {
        var coach = await LoginAs("coach1", "pass123");
        // Approve all 3
        await coach.PostAsync("/api/teams/progress/approve", null);
        await coach.PostAsync("/api/teams/progress/approve", null);
        await coach.PostAsync("/api/teams/progress/approve", null);

        // Fourth approve should conflict
        var response = await coach.PostAsync("/api/teams/progress/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // POST /api/teams/progress/revert

    [Fact]
    public async Task Revert_AsCoach_GoesBackToPreviousChallenge()
    {
        var coach = await LoginAs("coach1", "pass123");
        await coach.PostAsync("/api/teams/progress/approve", null);

        var response = await coach.PostAsync("/api/teams/progress/revert", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currentStep").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Revert_AtFirstChallenge_Returns409()
    {
        // Reset to ensure we're at step 1
        var coach = await LoginAs("coach1", "pass123");
        await coach.PostAsync("/api/teams/progress/reset", null);

        var response = await coach.PostAsync("/api/teams/progress/revert", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Revert_AsParticipant_Returns403()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.PostAsync("/api/teams/progress/revert", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // POST /api/teams/progress/reset

    [Fact]
    public async Task Reset_AsCoach_ResetsToChallenge1()
    {
        var coach = await LoginAs("coach1", "pass123");
        await coach.PostAsync("/api/teams/progress/approve", null);

        var response = await coach.PostAsync("/api/teams/progress/reset", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currentStep").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Reset_AlreadyAtOne_Returns200()
    {
        var coach = await LoginAs("coach1", "pass123");
        await coach.PostAsync("/api/teams/progress/reset", null);

        var response = await coach.PostAsync("/api/teams/progress/reset", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_AsParticipant_Returns403()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.PostAsync("/api/teams/progress/reset", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
