using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests.Integration;

[Trait("Category", "Integration")]
public class MicrohackManagementEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MicrohackManagementEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MicrohackCrudListAndToggleFlow_AsTechlead_Succeeds()
    {
        var techlead = await LoginAs("techlead", "pass123");
        var microhackId = $"microhack-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        var createResponse = await techlead.PostAsJsonAsync("/api/admin/microhacks", new
        {
            microhackId,
            enabled = false,
            scheduleStart = now.AddHours(1),
            scheduleEnd = now.AddHours(2),
            timeZone = "UTC",
            contentPath = "hackcontent/custom",
            environmentReference = "env-1"
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("microhackId").GetString().Should().Be(microhackId);
        created.GetProperty("enabled").GetBoolean().Should().BeFalse();

        var listResponse = await techlead.GetAsync("/api/admin/microhacks");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var microhacks = await listResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        microhacks.Should().NotBeNull();
        microhacks!.Should().Contain(item => item.GetProperty("microhackId").GetString() == microhackId);

        var updateResponse = await techlead.PutAsJsonAsync($"/api/admin/microhacks/{microhackId}", new
        {
            enabled = true,
            scheduleStart = now.AddHours(-1),
            scheduleEnd = now.AddHours(3),
            timeZone = "Pacific Standard Time",
            contentPath = "hackcontent/updated",
            environmentReference = "env-2"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("enabled").GetBoolean().Should().BeTrue();
        updated.GetProperty("timeZone").GetString().Should().Be("Pacific Standard Time");

        var disableResponse = await techlead.PostAsync($"/api/admin/microhacks/{microhackId}/disable", null);
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var disabled = await disableResponse.Content.ReadFromJsonAsync<JsonElement>();
        disabled.GetProperty("enabled").GetBoolean().Should().BeFalse();

        var enableResponse = await techlead.PostAsync($"/api/admin/microhacks/{microhackId}/enable", null);
        enableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var enabled = await enableResponse.Content.ReadFromJsonAsync<JsonElement>();
        enabled.GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DisabledOrOutOfWindowMicrohack_BlocksParticipantEndpoints()
    {
        var techlead = await LoginAs("techlead", "pass123");
        var microhackId = $"microhack-team-alpha-{Guid.NewGuid():N}";

        try
        {
            var createResponse = await CreateTeamAlphaMicrohack(techlead, microhackId, enabled: true, scheduleStart: null, scheduleEnd: null);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var disableResponse = await techlead.PostAsync($"/api/admin/microhacks/{microhackId}/disable", null);
            disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var participant = await LoginAs("hacker1", "pass123");

            foreach (var endpoint in new[] { "/api/challenges", "/api/credentials", "/api/timer", "/api/lab" })
            {
                var blockedResponse = await participant.GetAsync(endpoint);
                blockedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
                var blockedBody = await blockedResponse.Content.ReadFromJsonAsync<JsonElement>();
                blockedBody.GetProperty("error").GetString().Should().Contain("unavailable");
            }

            var now = DateTime.UtcNow;
            var scheduledResponse = await UpdateMicrohack(techlead, microhackId, enabled: true, scheduleStart: now.AddHours(1), scheduleEnd: now.AddHours(2), assignTeamAlpha: true);
            scheduledResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var outsideScheduleResponse = await participant.GetAsync("/api/challenges");
            outsideScheduleResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var outsideScheduleBody = await outsideScheduleResponse.Content.ReadFromJsonAsync<JsonElement>();
            outsideScheduleBody.GetProperty("error").GetString().Should().Contain("scheduled window");
        }
        finally
        {
            await RestoreTeamAlpha(techlead, microhackId);
        }
    }

    [Fact]
    public async Task EnabledInWindowMicrohack_AllowsParticipantEndpoints()
    {
        var techlead = await LoginAs("techlead", "pass123");
        var microhackId = $"microhack-team-alpha-{Guid.NewGuid():N}";

        try
        {
            var now = DateTime.UtcNow;
            var createResponse = await CreateTeamAlphaMicrohack(techlead, microhackId, enabled: true, scheduleStart: now.AddHours(-1), scheduleEnd: now.AddHours(1));
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var participant = await LoginAs("hacker1", "pass123");

            (await participant.GetAsync("/api/challenges")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await participant.GetAsync("/api/credentials")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await participant.GetAsync("/api/timer")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await participant.GetAsync("/api/lab")).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }
        finally
        {
            await RestoreTeamAlpha(techlead, microhackId);
        }
    }

    private async Task<HttpClient> LoginAs(string username, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var session = cookies.First(cookie => cookie.StartsWith("hackbox_session="));
            client.DefaultRequestHeaders.Add("Cookie", session.Split(';')[0]);
        }

        return client;
    }

    private Task<HttpResponseMessage> CreateTeamAlphaMicrohack(HttpClient techlead, string microhackId, bool enabled, DateTime? scheduleStart, DateTime? scheduleEnd)
    {
        return techlead.PostAsJsonAsync("/api/admin/microhacks", new
        {
            microhackId,
            enabled,
            scheduleStart,
            scheduleEnd,
            timeZone = "UTC",
            contentPath = "hackcontent",
            environmentReference = "env-team-alpha",
            teams = new[] { "team-alpha" }
        });
    }

    private Task<HttpResponseMessage> UpdateMicrohack(HttpClient techlead, string microhackId, bool enabled, DateTime? scheduleStart, DateTime? scheduleEnd, bool assignTeamAlpha)
    {
        return techlead.PutAsJsonAsync($"/api/admin/microhacks/{microhackId}", new
        {
            enabled,
            scheduleStart,
            scheduleEnd,
            timeZone = "UTC",
            contentPath = "hackcontent",
            environmentReference = "env-team-alpha",
            teams = assignTeamAlpha ? new[] { "team-alpha" } : Array.Empty<string>()
        });
    }

    private async Task RestoreTeamAlpha(HttpClient techlead, string microhackId)
    {
        var response = await UpdateMicrohack(techlead, microhackId, enabled: true, scheduleStart: null, scheduleEnd: null, assignTeamAlpha: false);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}
