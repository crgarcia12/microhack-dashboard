using Api.Hubs;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Api.Endpoints;

public static class ChallengeEndpoints
{
    public static void MapChallengeEndpoints(this WebApplication app)
    {
        // Challenge list and detail
        app.MapGet("/api/challenges", HandleGetChallenges)
           .WithName("GetChallenges")
           .WithTags("Challenges");

        app.MapGet("/api/challenges/{number:int}", HandleGetChallenge)
           .WithName("GetChallenge")
           .WithTags("Challenges");

        app.MapGet("/api/challenges/media/{filename}", HandleGetMedia)
           .WithName("GetChallengeMedia")
           .WithTags("Challenges");

        // Team progress
        app.MapGet("/api/teams/progress", HandleGetProgress)
           .WithName("GetProgress")
           .WithTags("Progress");

        app.MapPost("/api/teams/progress/approve", HandleApprove)
           .WithName("Approve")
           .WithTags("Progress");

        app.MapPost("/api/teams/progress/revert", HandleRevert)
           .WithName("Revert")
           .WithTags("Progress");

        app.MapPost("/api/teams/progress/reset", HandleReset)
           .WithName("Reset")
           .WithTags("Progress");
    }

    private static IResult HandleGetChallenges(HttpContext context, IChallengeService challengeService)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);

        var teamId = session.Team ?? "";
        var progress = challengeService.GetTeamProgress(teamId);
        var challenges = challengeService.GetChallenges();

        var result = challenges.Select(c =>
        {
            var status = ChallengeService.ComputeStatus(c.Number, progress.CurrentStep);
            return new ChallengeListItem
            {
                ChallengeNumber = c.Number,
                Title = status == "locked" ? null : c.Title,
                Status = status
            };
        }).ToList();

        return Results.Ok(result);
    }

    private static IResult HandleGetChallenge(int number, HttpContext context, IChallengeService challengeService)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);

        var challenge = challengeService.GetChallenge(number);
        if (challenge == null)
            return Results.Json(new { error = "Challenge not found" }, statusCode: 404);

        var teamId = session.Team ?? "";
        var progress = challengeService.GetTeamProgress(teamId);
        var status = ChallengeService.ComputeStatus(number, progress.CurrentStep);

        if (status == "locked")
            return Results.Json(new { error = "Challenge is locked" }, statusCode: 403);

        return Results.Ok(new ChallengeDetail
        {
            ChallengeNumber = challenge.Number,
            Title = challenge.Title,
            ContentHtml = challenge.RawMarkdown
        });
    }

    private static IResult HandleGetMedia(string filename, HttpContext context, IWebHostEnvironment env)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);

        // Prevent path traversal
        if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            return Results.Json(new { error = "File not found" }, statusCode: 404);

        var challengesDir = Path.Combine(env.ContentRootPath, "hackcontent", "challenges");
        var filePath = Path.GetFullPath(Path.Combine(challengesDir, filename));

        // Ensure the resolved path is within the challenges directory
        var resolvedDir = Path.GetFullPath(challengesDir);
        if (!filePath.StartsWith(resolvedDir))
            return Results.Json(new { error = "File not found" }, statusCode: 404);

        if (!File.Exists(filePath))
            return Results.Json(new { error = "File not found" }, statusCode: 404);

        return Results.File(filePath);
    }

    private static IResult HandleGetProgress(HttpContext context, IChallengeService challengeService)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);

        var teamId = session.Team ?? "";
        var progress = challengeService.GetTeamProgress(teamId);
        return Results.Ok(progress);
    }

    private static async Task<IResult> HandleApprove(
        HttpContext context,
        IChallengeService challengeService,
        IHubContext<ChallengeHub> hubContext)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);

        if (session.Role != "coach" && session.Role != "organizer")
            return Results.Json(new { error = "Insufficient permissions" }, statusCode: 403);

        var teamId = session.Team ?? "";
        var (progress, error) = challengeService.Approve(teamId);

        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        await hubContext.Clients.Group(teamId).SendAsync("progressUpdated", progress);
        return Results.Ok(progress);
    }

    private static async Task<IResult> HandleRevert(
        HttpContext context,
        IChallengeService challengeService,
        IHubContext<ChallengeHub> hubContext)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);

        if (session.Role != "coach" && session.Role != "organizer")
            return Results.Json(new { error = "Insufficient permissions" }, statusCode: 403);

        var teamId = session.Team ?? "";
        var (progress, error) = challengeService.Revert(teamId);

        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        await hubContext.Clients.Group(teamId).SendAsync("progressUpdated", progress);
        return Results.Ok(progress);
    }

    private static async Task<IResult> HandleReset(
        HttpContext context,
        IChallengeService challengeService,
        IHubContext<ChallengeHub> hubContext)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);

        if (session.Role != "coach" && session.Role != "organizer")
            return Results.Json(new { error = "Insufficient permissions" }, statusCode: 403);

        var teamId = session.Team ?? "";
        var (progress, error) = challengeService.Reset(teamId);

        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        await hubContext.Clients.Group(teamId).SendAsync("progressUpdated", progress);
        return Results.Ok(progress);
    }
}
