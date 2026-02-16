using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration;

[Trait("Category", "Integration")]
public class DashboardEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _tempDir;

    public DashboardEndpointTests(WebApplicationFactory<Program> factory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dashboard_int_tests_{Guid.NewGuid():N}");
        var challengesDir = Path.Combine(_tempDir, "challenges");
        var progressDir = Path.Combine(_tempDir, "progress");
        var timerDir = Path.Combine(_tempDir, "timerdata");
        Directory.CreateDirectory(challengesDir);
        Directory.CreateDirectory(progressDir);
        Directory.CreateDirectory(timerDir);

        File.WriteAllText(Path.Combine(challengesDir, "challenge-001.md"), "# Challenge 1\nContent");
        File.WriteAllText(Path.Combine(challengesDir, "challenge-002.md"), "# Challenge 2\nContent");
        File.WriteAllText(Path.Combine(challengesDir, "challenge-003.md"), "# Challenge 3\nContent");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var csDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IChallengeService));
                if (csDesc != null) services.Remove(csDesc);
                services.AddSingleton<IChallengeService>(sp =>
                {
                    var repo = new Api.Data.File.FileProgressRepository(progressDir,
                        NullLogger<Api.Data.File.FileProgressRepository>.Instance);
                    return new ChallengeService(challengesDir, repo,
                        NullLogger<ChallengeService>.Instance);
                });

                var tsDesc = services.FirstOrDefault(d => d.ServiceType == typeof(ITimerService));
                if (tsDesc != null) services.Remove(tsDesc);
                services.AddSingleton<ITimerService>(sp =>
                {
                    var timerRepo = new Api.Data.File.FileTimerRepository(timerDir,
                        NullLogger<Api.Data.File.FileTimerRepository>.Instance);
                    return new TimerService(timerRepo, NullLogger<TimerService>.Instance);
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

    // GET /api/admin/teams

    [Fact]
    public async Task GetTeams_AsOrganizer_Returns200WithAllTeams()
    {
        var client = await LoginAs("techlead", "pass123");
        var response = await client.GetAsync("/api/admin/teams");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("teams").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTeams_AsParticipant_Returns403()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/admin/teams");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeams_AsCoach_Returns403()
    {
        var client = await LoginAs("coach1", "pass123");
        var response = await client.GetAsync("/api/admin/teams");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeams_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/teams");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTeams_IncludesTotalChallengesCount()
    {
        var client = await LoginAs("techlead", "pass123");
        var response = await client.GetAsync("/api/admin/teams");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalChallenges").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task GetTeams_IncludesTimerStatusPerTeam()
    {
        var client = await LoginAs("techlead", "pass123");
        var response = await client.GetAsync("/api/admin/teams");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var firstTeam = body.GetProperty("teams").EnumerateArray().First();
        firstTeam.TryGetProperty("manualTimerStatus", out _).Should().BeTrue();
        firstTeam.TryGetProperty("elapsedSeconds", out _).Should().BeTrue();
    }

    // POST /api/admin/teams/{teamId}/challenges/approve

    [Fact]
    public async Task ChallengeAction_Advance_MovesTeamForward()
    {
        var client = await LoginAs("techlead", "pass123");
        var response = await client.PostAsync("/api/admin/teams/team-alpha/challenges/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currentStep").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ChallengeAction_Revert_MovesTeamBack()
    {
        var client = await LoginAs("techlead", "pass123");
        // Advance first, then revert
        await client.PostAsync("/api/admin/teams/team-alpha/challenges/approve", null);
        var response = await client.PostAsync("/api/admin/teams/team-alpha/challenges/revert", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChallengeAction_Reset_ResetsTeamToStep1()
    {
        var client = await LoginAs("techlead", "pass123");
        await client.PostAsync("/api/admin/teams/team-alpha/challenges/approve", null);

        var response = await client.PostAsync("/api/admin/teams/team-alpha/challenges/reset", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currentStep").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ChallengeAction_AsNonOrganizer_Returns403()
    {
        var client = await LoginAs("coach1", "pass123");
        var response = await client.PostAsync("/api/admin/teams/team-alpha/challenges/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Bulk challenge operations

    [Fact]
    public async Task BulkAdvance_AsOrganizer_AdvancesAllTeams()
    {
        var client = await LoginAs("techlead", "pass123");
        // Reset all teams first
        await client.PostAsync("/api/admin/challenges/reset-all", null);

        var response = await client.PostAsync("/api/admin/challenges/approve-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("action").GetString().Should().Be("approve");
        body.GetProperty("results").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkRevert_AsOrganizer_RevertsAllTeams()
    {
        var client = await LoginAs("techlead", "pass123");
        // Advance all first
        await client.PostAsync("/api/admin/challenges/approve-all", null);

        var response = await client.PostAsync("/api/admin/challenges/revert-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("action").GetString().Should().Be("revert");
    }

    [Fact]
    public async Task BulkReset_AsOrganizer_ResetsAllTeams()
    {
        var client = await LoginAs("techlead", "pass123");
        var response = await client.PostAsync("/api/admin/challenges/reset-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("action").GetString().Should().Be("reset");
    }

    [Fact]
    public async Task BulkOperation_PartialFailure_ReturnsPerTeamResults()
    {
        var client = await LoginAs("techlead", "pass123");
        // Reset all to step 1, then try to revert — all should fail (already at step 1)
        await client.PostAsync("/api/admin/challenges/reset-all", null);

        var response = await client.PostAsync("/api/admin/challenges/revert-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var results = body.GetProperty("results").EnumerateArray().ToList();
        results.Should().NotBeEmpty();
        // Each team result should have teamName and success fields
        foreach (var result in results)
        {
            result.TryGetProperty("teamName", out _).Should().BeTrue();
            result.TryGetProperty("success", out _).Should().BeTrue();
        }
    }

    // Per-team timer operations

    [Fact]
    public async Task TimerStart_AsOrganizer_StartsTeamTimer()
    {
        var client = await LoginAs("techlead", "pass123");
        // Reset first to ensure stopped state
        await client.PostAsync("/api/admin/teams/team-alpha/timer/reset", null);

        var response = await client.PostAsync("/api/admin/teams/team-alpha/timer/start", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("running");
    }

    [Fact]
    public async Task TimerStop_AsOrganizer_StopsTeamTimer()
    {
        var client = await LoginAs("techlead", "pass123");
        await client.PostAsync("/api/admin/teams/team-alpha/timer/reset", null);
        await client.PostAsync("/api/admin/teams/team-alpha/timer/start", null);

        var response = await client.PostAsync("/api/admin/teams/team-alpha/timer/stop", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("stopped");
    }

    [Fact]
    public async Task TimerReset_AsOrganizer_ResetsTeamTimer()
    {
        var client = await LoginAs("techlead", "pass123");
        var response = await client.PostAsync("/api/admin/teams/team-alpha/timer/reset", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("stopped");
        body.GetProperty("elapsed").GetInt32().Should().Be(0);
    }

    // Bulk timer operations

    [Fact]
    public async Task BulkTimerStart_AsOrganizer_StartsAllTimers()
    {
        var client = await LoginAs("techlead", "pass123");
        // Prime timer state for teams by accessing their timers individually
        await client.PostAsync("/api/admin/teams/team-alpha/timer/reset", null);
        await client.PostAsync("/api/admin/teams/team-beta/timer/reset", null);

        var response = await client.PostAsync("/api/admin/timer/start-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("results").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkTimerStop_AsOrganizer_StopsAllTimers()
    {
        var client = await LoginAs("techlead", "pass123");
        // Prime and start timers first
        await client.PostAsync("/api/admin/teams/team-alpha/timer/reset", null);
        await client.PostAsync("/api/admin/teams/team-beta/timer/reset", null);
        await client.PostAsync("/api/admin/timer/start-all", null);

        var response = await client.PostAsync("/api/admin/timer/stop-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("results").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkTimerReset_AsOrganizer_ResetsAllTimers()
    {
        var client = await LoginAs("techlead", "pass123");
        // Prime timer state for teams
        await client.PostAsync("/api/admin/teams/team-alpha/timer/reset", null);
        await client.PostAsync("/api/admin/teams/team-beta/timer/reset", null);

        var response = await client.PostAsync("/api/admin/timer/reset-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("results").GetArrayLength().Should().BeGreaterThan(0);
    }
}
