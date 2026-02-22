using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests.Integration;

[Trait("Category", "Integration")]
public class HackStateEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HackStateEndpointTests(WebApplicationFactory<Program> factory)
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

    [Fact]
    public async Task GetDatastore_AsTechlead_ReturnsProviderAndTarget()
    {
        var client = await LoginAs("techlead", "pass123");
        var response = await client.GetAsync("/api/hack/datastore");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("provider").GetString().Should().BeOneOf("Sqlite", "SqlServer");
        body.GetProperty("target").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetDatastore_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/hack/datastore");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDatastore_AsParticipant_Returns403()
    {
        var client = await LoginAs("hacker1", "pass123");
        var response = await client.GetAsync("/api/hack/datastore");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
