using Api.Models;
using Api.Services;

namespace Api.Endpoints;

public static class CredentialEndpoints
{
    public static void MapCredentialEndpoints(this WebApplication app)
    {
        app.MapGet("/api/credentials", HandleGetCredentials)
           .WithName("GetCredentials")
           .WithTags("Credentials");
    }

    private static IResult HandleGetCredentials(HttpContext context, ICredentialService credentialService)
    {
        // CRED-012: Unauthenticated requests receive 401
        if (context.Items["User"] is not AuthSession session)
        {
            return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
        }

        // CRED-013: Organizer-role requests receive 403
        if (session.Role == "techlead")
        {
            return Results.Json(new { error = "Credentials are not available for organizer accounts." }, statusCode: 403);
        }

        // CRED-011: Team identity derived from session
        var teamName = session.Team ?? string.Empty;
        var credentials = credentialService.GetCredentials(teamName);

        return Results.Ok(new
        {
            teamName = credentials.TeamName,
            categories = credentials.Categories.Select(c => new
            {
                name = c.Name,
                credentials = c.Credentials.Select(i => new
                {
                    label = i.Label,
                    value = i.Value
                })
            })
        });
    }
}
