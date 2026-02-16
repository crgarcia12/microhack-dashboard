using Api.Models;
using Api.Services;

namespace Api.Endpoints;

public static class TimerEndpoints
{
    public static void MapTimerEndpoints(this WebApplication app)
    {
        // Team-scoped endpoints
        app.MapGet("/api/timer", HandleGetTimer)
           .WithName("GetTimer")
           .WithTags("Timer");

        app.MapPost("/api/timer/start", HandleStart)
           .WithName("StartTimer")
           .WithTags("Timer");

        app.MapPost("/api/timer/stop", HandleStop)
           .WithName("StopTimer")
           .WithTags("Timer");

        app.MapPost("/api/timer/reset", HandleReset)
           .WithName("ResetTimer")
           .WithTags("Timer");

        // Organizer: per-team
        app.MapGet("/api/admin/teams/{teamName}/timer", HandleAdminGetTimer)
           .WithName("AdminGetTimer")
           .WithTags("Timer", "Admin");

        app.MapPost("/api/admin/teams/{teamName}/timer/start", HandleAdminStart)
           .WithName("AdminStartTimer")
           .WithTags("Timer", "Admin");

        app.MapPost("/api/admin/teams/{teamName}/timer/stop", HandleAdminStop)
           .WithName("AdminStopTimer")
           .WithTags("Timer", "Admin");

        app.MapPost("/api/admin/teams/{teamName}/timer/reset", HandleAdminReset)
           .WithName("AdminResetTimer")
           .WithTags("Timer", "Admin");

        // Organizer: bulk
        app.MapPost("/api/admin/timer/start-all", HandleAdminStartAll)
           .WithName("AdminStartAllTimers")
           .WithTags("Timer", "Admin");

        app.MapPost("/api/admin/timer/stop-all", HandleAdminStopAll)
           .WithName("AdminStopAllTimers")
           .WithTags("Timer", "Admin");

        app.MapPost("/api/admin/timer/reset-all", HandleAdminResetAll)
           .WithName("AdminResetAllTimers")
           .WithTags("Timer", "Admin");
    }

    // --- Team-scoped handlers ---

    private static IResult HandleGetTimer(HttpContext context, ITimerService timerService)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var teamName = session.Team ?? "";
        var state = timerService.GetTimerState(teamName);
        return Results.Ok(FormatTimerResponse(state));
    }

    private static IResult HandleStart(HttpContext context, ITimerService timerService)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var teamName = session.Team ?? "";
        var (result, error) = timerService.StartManualTimer(teamName);
        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        return Results.Ok(FormatManualTimerResponse(result!));
    }

    private static IResult HandleStop(HttpContext context, ITimerService timerService)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var teamName = session.Team ?? "";
        var (result, error) = timerService.StopManualTimer(teamName);
        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        return Results.Ok(FormatManualTimerResponse(result!));
    }

    private static IResult HandleReset(HttpContext context, ITimerService timerService)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var teamName = session.Team ?? "";
        var result = timerService.ResetManualTimer(teamName);
        return Results.Ok(FormatManualTimerResponse(result));
    }

    // --- Organizer per-team handlers ---

    private static IResult HandleAdminGetTimer(string teamName, HttpContext context, ITimerService timerService, IAuthService authService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        if (!authService.GetAllTeams().Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var state = timerService.GetTimerState(teamName);
        return Results.Ok(FormatTimerResponse(state));
    }

    private static IResult HandleAdminStart(string teamName, HttpContext context, ITimerService timerService, IAuthService authService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        if (!authService.GetAllTeams().Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var (result, error) = timerService.StartManualTimer(teamName);
        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        return Results.Ok(FormatManualTimerResponse(result!));
    }

    private static IResult HandleAdminStop(string teamName, HttpContext context, ITimerService timerService, IAuthService authService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        if (!authService.GetAllTeams().Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var (result, error) = timerService.StopManualTimer(teamName);
        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        return Results.Ok(FormatManualTimerResponse(result!));
    }

    private static IResult HandleAdminReset(string teamName, HttpContext context, ITimerService timerService, IAuthService authService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        if (!authService.GetAllTeams().Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var result = timerService.ResetManualTimer(teamName);
        return Results.Ok(FormatManualTimerResponse(result));
    }

    // --- Organizer bulk handlers ---

    private static IResult HandleAdminStartAll(HttpContext context, ITimerService timerService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var allStates = timerService.GetAllTimerStates();
        var results = new List<object>();
        foreach (var state in allStates)
        {
            var (result, error) = timerService.StartManualTimer(state.TeamName);
            if (error != null)
                results.Add(new { teamId = state.TeamName, status = "conflict", error });
            else
                results.Add(new { teamId = state.TeamName, status = "ok", error = "" });
        }
        return Results.Ok(new { results });
    }

    private static IResult HandleAdminStopAll(HttpContext context, ITimerService timerService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var allStates = timerService.GetAllTimerStates();
        var results = new List<object>();
        foreach (var state in allStates)
        {
            var (result, error) = timerService.StopManualTimer(state.TeamName);
            if (error != null)
                results.Add(new { teamId = state.TeamName, status = "conflict", error });
            else
                results.Add(new { teamId = state.TeamName, status = "ok", error = "" });
        }
        return Results.Ok(new { results });
    }

    private static IResult HandleAdminResetAll(HttpContext context, ITimerService timerService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var allStates = timerService.GetAllTimerStates();
        foreach (var state in allStates)
        {
            timerService.ResetManualTimer(state.TeamName);
        }
        var results = allStates.Select(s => new { teamId = s.TeamName, status = "ok" }).ToList();
        return Results.Ok(new { results });
    }

    // --- Helpers ---

    private static IResult? RequireOrganizer(HttpContext context)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);
        if (session.Role != "techlead")
            return Results.Json(new { error = "Forbidden" }, statusCode: 403);
        return null;
    }

    private static object FormatTimerResponse(TimerState state)
    {
        return new
        {
            automatic = new
            {
                timerStartedAt = state.TimerStartedAt?.ToString("o"),
                challengeTimes = state.ChallengeTimes
            },
            manual = new
            {
                status = state.ManualTimer.Status,
                startedAt = state.ManualTimer.StartedAt?.ToString("o"),
                elapsed = state.ManualTimer.AccumulatedSeconds
            }
        };
    }

    private static object FormatManualTimerResponse(ManualTimerState manual)
    {
        return new
        {
            status = manual.Status,
            startedAt = manual.StartedAt?.ToString("o"),
            elapsed = manual.AccumulatedSeconds
        };
    }
}
