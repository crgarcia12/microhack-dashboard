using Api.Models;
using Microsoft.Extensions.Options;

namespace Api.Endpoints;

public static class LabEndpoints
{
    public static void MapLabEndpoints(this WebApplication app)
    {
        app.MapGet("/api/lab", HandleGetLab)
           .WithName("GetLab")
           .WithTags("Lab");
    }

    private static IResult HandleGetLab(HttpContext context, IOptions<LabConfig> labOptions)
    {
        if (context.Items["User"] is not AuthSession)
        {
            return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
        }

        var config = labOptions.Value;

        if (!config.Enabled)
        {
            return Results.NotFound(new { error = "Lab environment is not enabled" });
        }

        return Results.Ok(new
        {
            enabled = config.Enabled,
            endpoints = config.Endpoints
        });
    }
}
