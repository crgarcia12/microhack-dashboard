using Api.Hubs;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        // Team status overview
        app.MapGet("/api/admin/teams", HandleGetTeams)
           .WithName("AdminGetTeams")
           .WithTags("Dashboard", "Admin");

        // Per-team challenge operations
        app.MapPost("/api/admin/teams/{teamName}/challenges/approve", HandleApprove)
           .WithName("AdminApproveChallenge")
           .WithTags("Dashboard", "Admin");

        app.MapPost("/api/admin/teams/{teamName}/challenges/revert", HandleRevert)
           .WithName("AdminRevertChallenge")
           .WithTags("Dashboard", "Admin");

        app.MapPost("/api/admin/teams/{teamName}/challenges/reset", HandleReset)
           .WithName("AdminResetChallenge")
           .WithTags("Dashboard", "Admin");

        // Bulk challenge operations
        app.MapPost("/api/admin/challenges/approve-all", HandleApproveAll)
           .WithName("AdminApproveAllChallenges")
           .WithTags("Dashboard", "Admin");

        app.MapPost("/api/admin/challenges/revert-all", HandleRevertAll)
           .WithName("AdminRevertAllChallenges")
           .WithTags("Dashboard", "Admin");

        app.MapPost("/api/admin/challenges/reset-all", HandleResetAll)
           .WithName("AdminResetAllChallenges")
           .WithTags("Dashboard", "Admin");
    }

    private static IResult HandleGetTeams(
        HttpContext context,
        IAuthService authService,
        IChallengeService challengeService,
        ITimerService timerService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var teams = authService.GetAllTeams();
        var totalChallenges = challengeService.TotalChallenges;

        var teamStatuses = teams.Select(teamName =>
        {
            var progress = challengeService.GetTeamProgress(teamName);
            var timerState = timerService.GetTimerState(teamName);
            var elapsed = ComputeElapsedSeconds(timerState.ManualTimer);

            return new TeamStatus
            {
                TeamName = teamName,
                CurrentStep = progress.CurrentStep,
                TotalChallenges = totalChallenges,
                IsCompleted = progress.Completed,
                ManualTimerStatus = timerState.ManualTimer.Status,
                ElapsedSeconds = elapsed,
                ChallengeTimes = timerState.ChallengeTimes
            };
        }).ToList();

        return Results.Ok(new { totalChallenges, teams = teamStatuses });
    }

    private static async Task<IResult> HandleApprove(
        string teamName,
        HttpContext context,
        IChallengeService challengeService,
        IHubContext<ChallengeHub> hubContext,
        IAuthService authService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        if (!authService.GetAllTeams().Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var (progress, error) = challengeService.Approve(teamName);
        if (error != null)
            return Results.Json(new { error }, statusCode: 400);

        await hubContext.Clients.Group(teamName).SendAsync("progressUpdated", progress);
        return Results.Ok(progress);
    }

    private static async Task<IResult> HandleRevert(
        string teamName,
        HttpContext context,
        IChallengeService challengeService,
        IHubContext<ChallengeHub> hubContext,
        IAuthService authService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        if (!authService.GetAllTeams().Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var (progress, error) = challengeService.Revert(teamName);
        if (error != null)
            return Results.Json(new { error }, statusCode: 400);

        await hubContext.Clients.Group(teamName).SendAsync("progressUpdated", progress);
        return Results.Ok(progress);
    }

    private static async Task<IResult> HandleReset(
        string teamName,
        HttpContext context,
        IChallengeService challengeService,
        IHubContext<ChallengeHub> hubContext,
        IAuthService authService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        if (!authService.GetAllTeams().Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var (progress, error) = challengeService.Reset(teamName);
        if (error != null)
            return Results.Json(new { error }, statusCode: 400);

        await hubContext.Clients.Group(teamName).SendAsync("progressUpdated", progress);
        return Results.Ok(progress);
    }

    private static async Task<IResult> HandleApproveAll(
        HttpContext context,
        IAuthService authService,
        IChallengeService challengeService,
        IHubContext<ChallengeHub> hubContext)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var results = await ExecuteBulkChallengeOp(authService, challengeService, hubContext, "approve");
        return Results.Ok(new { action = "approve", results });
    }

    private static async Task<IResult> HandleRevertAll(
        HttpContext context,
        IAuthService authService,
        IChallengeService challengeService,
        IHubContext<ChallengeHub> hubContext)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var results = await ExecuteBulkChallengeOp(authService, challengeService, hubContext, "revert");
        return Results.Ok(new { action = "revert", results });
    }

    private static async Task<IResult> HandleResetAll(
        HttpContext context,
        IAuthService authService,
        IChallengeService challengeService,
        IHubContext<ChallengeHub> hubContext)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var results = await ExecuteBulkChallengeOp(authService, challengeService, hubContext, "reset");
        return Results.Ok(new { action = "reset", results });
    }

    private static async Task<List<BulkOperationResult>> ExecuteBulkChallengeOp(
        IAuthService authService,
        IChallengeService challengeService,
        IHubContext<ChallengeHub> hubContext,
        string action)
    {
        var teams = authService.GetAllTeams();
        var results = new List<BulkOperationResult>();

        foreach (var teamName in teams)
        {
            var (progress, error) = action switch
            {
                "approve" => challengeService.Approve(teamName),
                "revert" => challengeService.Revert(teamName),
                "reset" => challengeService.Reset(teamName),
                _ => (null, $"Invalid action: {action}")
            };

            if (error == null && progress != null)
            {
                await hubContext.Clients.Group(teamName).SendAsync("progressUpdated", progress);
            }

            results.Add(new BulkOperationResult
            {
                TeamName = teamName,
                Success = error == null,
                Error = error
            });
        }

        return results;
    }

    private static int ComputeElapsedSeconds(ManualTimerState manual)
    {
        var elapsed = manual.AccumulatedSeconds;
        if (manual.Status == "running" && manual.StartedAt.HasValue)
        {
            elapsed += (int)(DateTime.UtcNow - manual.StartedAt.Value).TotalSeconds;
        }
        return elapsed;
    }

    private static IResult? RequireOrganizer(HttpContext context)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);
        if (session.Role != "techlead")
            return Results.Json(new { error = "Forbidden — organizer role required" }, statusCode: 403);
        return null;
    }
}
