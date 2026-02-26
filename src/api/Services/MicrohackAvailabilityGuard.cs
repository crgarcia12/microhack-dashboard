using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public static class MicrohackAvailabilityGuard
{
    public static IResult? EnsureAvailabilityForParticipation(HttpContext context, HackboxDbContext dbContext)
    {
        if (context.Items["User"] is not AuthSession session)
        {
            return Results.Json(new { error = "Authentication required" }, statusCode: 401);
        }

        if (session.Role == "techlead")
        {
            return null;
        }

        if (session.Role is not "participant" and not "coach")
        {
            return Results.Json(new { error = "Forbidden" }, statusCode: 403);
        }

        if (string.IsNullOrWhiteSpace(session.Team))
        {
            return Results.Json(new { error = "Forbidden — user must be assigned to a team." }, statusCode: 403);
        }

        var team = dbContext.Teams
            .AsNoTracking()
            .FirstOrDefault(candidate => candidate.Name == session.Team && !candidate.IsMicrohack);

        if (team == null)
        {
            return Results.Json(new { error = "Forbidden — assigned team was not found." }, statusCode: 403);
        }

        // Legacy compatibility: if a team is not yet assigned to a hackathon,
        // keep current behavior and allow participation.
        if (string.IsNullOrWhiteSpace(team.MicrohackId))
        {
            return null;
        }

        var microhack = dbContext.Teams
            .AsNoTracking()
            .FirstOrDefault(candidate => candidate.Name == team.MicrohackId && candidate.IsMicrohack);

        if (microhack == null)
        {
            return Results.Json(new { error = "Forbidden — assigned hackathon was not found." }, statusCode: 403);
        }

        if (!microhack.Enabled)
        {
            return Results.Json(new { error = $"Microhack '{microhack.Name}' is currently unavailable." }, statusCode: 403);
        }

        var nowUtc = DateTime.UtcNow;
        if (microhack.ScheduleStartUtc.HasValue && nowUtc < microhack.ScheduleStartUtc.Value)
        {
            return Results.Json(new { error = $"Microhack '{microhack.Name}' is currently unavailable outside its scheduled window." }, statusCode: 403);
        }

        if (microhack.ScheduleEndUtc.HasValue && nowUtc > microhack.ScheduleEndUtc.Value)
        {
            return Results.Json(new { error = $"Microhack '{microhack.Name}' is currently unavailable outside its scheduled window." }, statusCode: 403);
        }

        return null;
    }
}
