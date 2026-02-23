using Api.Hubs;
using Api.Models;
using Api.Services;
using Api.Data;
using Microsoft.AspNetCore.SignalR;

namespace Api.Endpoints;

public static class HackStateEndpoints
{
    public static void MapHackStateEndpoints(this WebApplication app)
    {
        // Get current hack state (public - all users need this)
        app.MapGet("/api/hack/state", HandleGetState)
           .WithName("GetHackState")
           .WithTags("HackState");

        // Get hack configuration (techlead only)
        app.MapGet("/api/hack/config", HandleGetConfig)
           .WithName("GetHackConfig")
           .WithTags("HackState", "Admin");

        // Get active datastore details (techlead only)
        app.MapGet("/api/hack/datastore", HandleGetDataStoreInfo)
           .WithName("GetHackDataStoreInfo")
           .WithTags("HackState", "Admin");

        // Save hack configuration (techlead only)
        app.MapPost("/api/hack/config", HandleSaveConfig)
           .WithName("SaveHackConfig")
           .WithTags("HackState", "Admin");

        // Launch hack (techlead only)
        app.MapPost("/api/hack/launch", HandleLaunchHack)
           .WithName("LaunchHack")
           .WithTags("HackState", "Admin");

        // Pause hack (techlead only)
        app.MapPost("/api/hack/pause", HandlePauseHack)
           .WithName("PauseHack")
           .WithTags("HackState", "Admin");
    }

    private static IResult HandleGetState(IHackStateService hackStateService)
    {
        return Results.Ok(BuildStatePayload(hackStateService));
    }

    private static IResult HandleGetConfig(
        HttpContext context,
        IHackStateService hackStateService)
    {
        var authResult = RequireTechLead(context);
        if (authResult != null) return authResult;

        var config = hackStateService.GetConfig();
        return Results.Ok(config);
    }

    private static IResult HandleGetDataStoreInfo(
        HttpContext context,
        DataStoreInfo dataStoreInfo)
    {
        var authResult = RequireTechLead(context);
        if (authResult != null) return authResult;

        return Results.Ok(dataStoreInfo);
    }

    private static async Task<IResult> HandleSaveConfig(
        HttpContext context,
        HackConfig config,
        IHackStateService hackStateService,
        IHubContext<ChallengeHub> hubContext)
    {
        var authResult = RequireTechLead(context);
        if (authResult != null) return authResult;

        var session = context.Items["User"] as AuthSession;
        hackStateService.SaveConfig(config, session!.Username);

        // Notify all clients that config was saved and state may have changed
        var state = BuildStatePayload(hackStateService);
        await hubContext.Clients.All.SendAsync("hackStateChanged", state);

        return Results.Ok(new { success = true, message = "Configuration saved" });
    }

    private static async Task<IResult> HandleLaunchHack(
        HttpContext context,
        IHackStateService hackStateService,
        IAuthService authService,
        IUserRepository userRepository,
        ITimerService timerService,
        IHubContext<ChallengeHub> hubContext)
    {
        var authResult = RequireOperator(context);
        if (authResult != null) return authResult;

        var session = context.Items["User"] as AuthSession;
        var launched = hackStateService.LaunchHack(session!.Username);
        if (!launched)
            return Results.Json(new { error = "Hack can only be started when it is not started or waiting." }, statusCode: 409);

        var config = hackStateService.GetConfig();
        foreach (var scope in HackModeHelper.GetDashboardScopes(config, authService, userRepository))
        {
            timerService.StartManualTimer(scope);
        }

        // Broadcast hack launch to all clients
        var state = BuildStatePayload(hackStateService);
        await hubContext.Clients.All.SendAsync("hackStateChanged", state);
        await hubContext.Clients.All.SendAsync("hackLaunched", state);

        return Results.Ok(new { success = true, message = "Hack launched!", startedAt = hackStateService.GetState().StartedAt });
    }

    private static async Task<IResult> HandlePauseHack(
        HttpContext context,
        IHackStateService hackStateService,
        IAuthService authService,
        IUserRepository userRepository,
        ITimerService timerService,
        IHubContext<ChallengeHub> hubContext)
    {
        var authResult = RequireOperator(context);
        if (authResult != null) return authResult;

        var session = context.Items["User"] as AuthSession;
        var paused = hackStateService.PauseHack(session!.Username);
        if (!paused)
            return Results.Json(new { error = "Hack is not currently active." }, statusCode: 409);

        var config = hackStateService.GetConfig();
        foreach (var scope in HackModeHelper.GetDashboardScopes(config, authService, userRepository))
        {
            timerService.StopManualTimer(scope);
        }

        var state = BuildStatePayload(hackStateService);
        await hubContext.Clients.All.SendAsync("hackStateChanged", state);

        return Results.Ok(new { success = true, message = "Hack paused" });
    }

    private static IResult? RequireTechLead(HttpContext context)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);
        if (session.Role != "techlead")
            return Results.Json(new { error = "Forbidden — techlead role required" }, statusCode: 403);
        return null;
    }

    private static IResult? RequireOperator(HttpContext context)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);
        if (session.Role != "techlead" && session.Role != "coach")
            return Results.Json(new { error = "Forbidden — operator role required" }, statusCode: 403);
        return null;
    }

    private static object BuildStatePayload(IHackStateService hackStateService)
    {
        var state = hackStateService.GetState();
        var config = hackStateService.GetConfig();

        return new
        {
            status = state.Status,
            startedAt = state.StartedAt,
            configuredBy = state.ConfiguredBy,
            updatedAt = state.UpdatedAt,
            mode = config.Mode,
            participantSolutionsVisible = HackModeHelper.IsParticipantSolutionsVisible(config)
        };
    }
}
