using Api.Models;
using Api.Services;
using Api.Data;

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

    private static IResult HandleGetTimer(HttpContext context, ITimerService timerService, IHackStateService hackStateService, HackboxDbContext dbContext)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var availability = MicrohackAvailabilityGuard.EnsureAvailabilityForParticipation(context, dbContext);
        if (availability != null) return availability;

        var config = hackStateService.GetConfig();
        var scope = HackModeHelper.ResolveProgressScope(session, config);
        var state = timerService.GetTimerState(scope);
        return Results.Ok(FormatTimerResponse(state));
    }

    private static IResult HandleStart(HttpContext context, ITimerService timerService, IHackStateService hackStateService, HackboxDbContext dbContext)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var availability = MicrohackAvailabilityGuard.EnsureAvailabilityForParticipation(context, dbContext);
        if (availability != null) return availability;

        var config = hackStateService.GetConfig();
        var scope = HackModeHelper.ResolveProgressScope(session, config);
        var (result, error) = timerService.StartManualTimer(scope);
        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        return Results.Ok(FormatManualTimerResponse(result!));
    }

    private static IResult HandleStop(HttpContext context, ITimerService timerService, IHackStateService hackStateService, HackboxDbContext dbContext)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var availability = MicrohackAvailabilityGuard.EnsureAvailabilityForParticipation(context, dbContext);
        if (availability != null) return availability;

        var config = hackStateService.GetConfig();
        var scope = HackModeHelper.ResolveProgressScope(session, config);
        var (result, error) = timerService.StopManualTimer(scope);
        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        return Results.Ok(FormatManualTimerResponse(result!));
    }

    private static IResult HandleReset(HttpContext context, ITimerService timerService, IHackStateService hackStateService, HackboxDbContext dbContext)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);

        var availability = MicrohackAvailabilityGuard.EnsureAvailabilityForParticipation(context, dbContext);
        if (availability != null) return availability;

        var config = hackStateService.GetConfig();
        var scope = HackModeHelper.ResolveProgressScope(session, config);
        var result = timerService.ResetManualTimer(scope);
        return Results.Ok(FormatManualTimerResponse(result));
    }

    // --- Organizer per-team handlers ---

    private static IResult HandleAdminGetTimer(
        string teamName,
        HttpContext context,
        ITimerService timerService,
        IAuthService authService,
        IUserRepository userRepository,
        IHackStateService hackStateService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var config = hackStateService.GetConfig();
        var scopes = HackModeHelper.GetDashboardScopes(config, authService, userRepository);
        if (!scopes.Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var state = timerService.GetTimerState(teamName);
        return Results.Ok(FormatTimerResponse(state));
    }

    private static IResult HandleAdminStart(
        string teamName,
        HttpContext context,
        ITimerService timerService,
        IAuthService authService,
        IUserRepository userRepository,
        IHackStateService hackStateService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var config = hackStateService.GetConfig();
        var scopes = HackModeHelper.GetDashboardScopes(config, authService, userRepository);
        if (!scopes.Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var (result, error) = timerService.StartManualTimer(teamName);
        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        return Results.Ok(FormatManualTimerResponse(result!));
    }

    private static IResult HandleAdminStop(
        string teamName,
        HttpContext context,
        ITimerService timerService,
        IAuthService authService,
        IUserRepository userRepository,
        IHackStateService hackStateService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var config = hackStateService.GetConfig();
        var scopes = HackModeHelper.GetDashboardScopes(config, authService, userRepository);
        if (!scopes.Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var (result, error) = timerService.StopManualTimer(teamName);
        if (error != null)
            return Results.Json(new { error }, statusCode: 409);

        return Results.Ok(FormatManualTimerResponse(result!));
    }

    private static IResult HandleAdminReset(
        string teamName,
        HttpContext context,
        ITimerService timerService,
        IAuthService authService,
        IUserRepository userRepository,
        IHackStateService hackStateService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var config = hackStateService.GetConfig();
        var scopes = HackModeHelper.GetDashboardScopes(config, authService, userRepository);
        if (!scopes.Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Json(new { error = "Team not found" }, statusCode: 404);

        var result = timerService.ResetManualTimer(teamName);
        return Results.Ok(FormatManualTimerResponse(result));
    }

    // --- Organizer bulk handlers ---

    private static IResult HandleAdminStartAll(
        HttpContext context,
        ITimerService timerService,
        IAuthService authService,
        IUserRepository userRepository,
        IHackStateService hackStateService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var results = new List<object>();
        var config = hackStateService.GetConfig();
        foreach (var teamName in HackModeHelper.GetDashboardScopes(config, authService, userRepository))
        {
            var (result, error) = timerService.StartManualTimer(teamName);
            if (error != null && !string.Equals(error, "Timer is already running", StringComparison.OrdinalIgnoreCase))
                results.Add(new { teamId = teamName, status = "conflict", error });
            else
                results.Add(new { teamId = teamName, status = "ok", error = "" });
        }
        return Results.Ok(new { results });
    }

    private static IResult HandleAdminStopAll(
        HttpContext context,
        ITimerService timerService,
        IAuthService authService,
        IUserRepository userRepository,
        IHackStateService hackStateService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var results = new List<object>();
        var config = hackStateService.GetConfig();
        foreach (var teamName in HackModeHelper.GetDashboardScopes(config, authService, userRepository))
        {
            var (result, error) = timerService.StopManualTimer(teamName);
            if (error != null && !string.Equals(error, "Timer is already stopped", StringComparison.OrdinalIgnoreCase))
                results.Add(new { teamId = teamName, status = "conflict", error });
            else
                results.Add(new { teamId = teamName, status = "ok", error = "" });
        }
        return Results.Ok(new { results });
    }

    private static IResult HandleAdminResetAll(
        HttpContext context,
        ITimerService timerService,
        IAuthService authService,
        IUserRepository userRepository,
        IHackStateService hackStateService)
    {
        var authResult = RequireOrganizer(context);
        if (authResult != null) return authResult;

        var config = hackStateService.GetConfig();
        var teamNames = HackModeHelper.GetDashboardScopes(config, authService, userRepository);
        foreach (var teamName in teamNames)
        {
            timerService.ResetManualTimer(teamName);
        }
        var results = teamNames.Select(teamName => new { teamId = teamName, status = "ok" }).ToList();
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
