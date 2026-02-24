using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests.Integration;

[Trait("Category", "Integration")]
public class UserManagementEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UserManagementEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ImportTeamsCsv_AsTechlead_UpsertsTeams()
    {
        var client = await LoginAs("techlead", "pass123");
        var newTeam = $"team-csv-{Guid.NewGuid():N}";
        using var payload = CreateCsvUpload($"name\r\nteam-alpha\r\n{newTeam}\r\n");

        var response = await client.PostAsync("/api/admin/team-admin/teams/import-csv", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var importResult = await response.Content.ReadFromJsonAsync<JsonElement>();
        importResult.GetProperty("created").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        importResult.GetProperty("updated").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var teamsResponse = await client.GetAsync("/api/admin/team-admin/teams");
        teamsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var teams = await teamsResponse.Content.ReadFromJsonAsync<List<string>>();
        teams.Should().NotBeNull();
        teams!.Should().Contain(newTeam);
    }

    [Fact]
    public async Task ImportUsersCsv_AsTechlead_UpsertsByUsername()
    {
        var client = await LoginAs("techlead", "pass123");
        var username = $"csv-user-{Guid.NewGuid():N}";
        var teamA = $"team-csv-a-{Guid.NewGuid():N}";
        var teamB = $"team-csv-b-{Guid.NewGuid():N}";

        using var createPayload = CreateCsvUpload($"username,password,role,team\r\n{username},Pass123!,participant,{teamA}\r\n");
        var createResponse = await client.PostAsync("/api/admin/team-admin/users/import-csv", createPayload);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createResult = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        createResult.GetProperty("created").GetInt32().Should().Be(1);
        createResult.GetProperty("updated").GetInt32().Should().Be(0);

        using var updatePayload = CreateCsvUpload($"username,role,team\r\n{username},coach,{teamB}\r\n");
        var updateResponse = await client.PostAsync("/api/admin/team-admin/users/import-csv", updatePayload);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        updateResult.GetProperty("created").GetInt32().Should().Be(0);
        updateResult.GetProperty("updated").GetInt32().Should().Be(1);

        var usersResponse = await client.GetAsync("/api/admin/team-admin/users");
        usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await usersResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        users.Should().NotBeNull();
        users!
            .Should()
            .Contain(u =>
                u.GetProperty("username").GetString() == username
                && u.GetProperty("role").GetString() == "coach"
                && u.GetProperty("team").GetString() == teamB);
    }

    [Fact]
    public async Task ImportUsersCsv_InIndividualMode_AutoSetsTeamToUsername()
    {
        await SetMode("individual");
        try
        {
            var client = await LoginAs("techlead", "pass123");
            var username = $"csv-individual-{Guid.NewGuid():N}";

            using var payload = CreateCsvUpload($"username,password,role\r\n{username},Pass123!,participant\r\n");
            var importResponse = await client.PostAsync("/api/admin/team-admin/users/import-csv", payload);

            importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var usersResponse = await client.GetAsync("/api/admin/team-admin/users");
            usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var users = await usersResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
            users.Should().NotBeNull();
            users!
                .Should()
                .Contain(u =>
                    u.GetProperty("username").GetString() == username
                    && u.GetProperty("team").GetString() == username);
        }
        finally
        {
            await SetMode("team");
        }
    }

    [Fact]
    public async Task CreateUser_InIndividualMode_AutoCreatesTeamWithUsername()
    {
        await SetMode("individual");
        try
        {
            var client = await LoginAs("techlead", "pass123");
            var username = $"individual-user-{Guid.NewGuid():N}";

            var createResponse = await client.PostAsJsonAsync("/api/admin/team-admin/users", new
            {
                username,
                password = "Pass123!",
                role = "participant"
            });

            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var usersResponse = await client.GetAsync("/api/admin/team-admin/users");
            usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var users = await usersResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
            users.Should().NotBeNull();
            users!
                .Should()
                .Contain(u =>
                    u.GetProperty("username").GetString() == username
                    && u.GetProperty("team").GetString() == username);

            var teamsResponse = await client.GetAsync("/api/admin/team-admin/teams");
            teamsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var teams = await teamsResponse.Content.ReadFromJsonAsync<List<string>>();
            teams.Should().NotBeNull();
            teams!.Should().Contain(username);
        }
        finally
        {
            await SetMode("team");
        }
    }

    [Fact]
    public async Task ExportTeamsCsv_AsTechlead_ReturnsCsv()
    {
        var client = await LoginAs("techlead", "pass123");

        var response = await client.GetAsync("/api/admin/team-admin/teams/export-csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("name");
        csv.Should().Contain("team-alpha");
    }

    [Fact]
    public async Task ExportUsersCsv_AsTechlead_ReturnsCsvWithoutPasswords()
    {
        var client = await LoginAs("techlead", "pass123");

        var response = await client.GetAsync("/api/admin/team-admin/users/export-csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("username,role,team");
        csv.Should().NotContain("password");
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

    private static MultipartFormDataContent CreateCsvUpload(string csv)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "import.csv");
        return content;
    }
}
