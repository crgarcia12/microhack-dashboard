using Reqnroll;
using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;

namespace Api.Tests.Features;

[Binding]
public class HealthSteps : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private HttpResponseMessage? _response;

    public HealthSteps(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [When(@"I check the health endpoint")]
    public async Task WhenICheckTheHealthEndpoint()
    {
        _response = await _client.GetAsync("/health");
    }

    [Then(@"the API should be healthy")]
    public void ThenTheApiShouldBeHealthy()
    {
        _response.Should().NotBeNull();
        _response!.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
