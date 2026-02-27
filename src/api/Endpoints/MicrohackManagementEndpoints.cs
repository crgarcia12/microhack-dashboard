using Api.Data;
using Api.Data.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public static class MicrohackManagementEndpoints
{
    public static void MapMicrohackManagementEndpoints(this WebApplication app)
    {
        app.MapGet("/api/admin/microhacks", HandleGetMicrohacks)
            .WithName("AdminGetMicrohacks")
            .WithTags("UserManagement", "Admin");

        app.MapPost("/api/admin/microhacks", HandleCreateMicrohack)
            .WithName("AdminCreateMicrohack")
            .WithTags("UserManagement", "Admin");

        app.MapPut("/api/admin/microhacks/{microhackId}", HandleUpdateMicrohack)
            .WithName("AdminUpdateMicrohack")
            .WithTags("UserManagement", "Admin");

        app.MapPost("/api/admin/microhacks/{microhackId}/enable", (string microhackId, HttpContext context, HackboxDbContext dbContext) =>
                HandleSetMicrohackEnabled(microhackId, true, context, dbContext))
            .WithName("AdminEnableMicrohack")
            .WithTags("UserManagement", "Admin");

        app.MapPost("/api/admin/microhacks/{microhackId}/disable", (string microhackId, HttpContext context, HackboxDbContext dbContext) =>
                HandleSetMicrohackEnabled(microhackId, false, context, dbContext))
            .WithName("AdminDisableMicrohack")
            .WithTags("UserManagement", "Admin");
    }

    private static IResult HandleGetMicrohacks(HttpContext context, HackboxDbContext dbContext)
    {
        var authResult = RequireTechlead(context);
        if (authResult != null) return authResult;

        var teamsByMicrohack = dbContext.Teams
            .AsNoTracking()
            .Where(team => !team.IsMicrohack && team.MicrohackId != null)
            .OrderBy(team => team.Name)
            .ToList()
            .GroupBy(team => team.MicrohackId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(team => team.Name).ToList(), StringComparer.OrdinalIgnoreCase);

        var microhacks = dbContext.Teams
            .AsNoTracking()
            .Where(team => team.IsMicrohack)
            .OrderBy(team => team.Name)
            .ToList()
            .Select(microhack =>
            {
                teamsByMicrohack.TryGetValue(microhack.Name, out var assignedTeams);
                return ToResponse(microhack, assignedTeams ?? new List<string>());
            })
            .ToList();

        return Results.Ok(microhacks);
    }

    private static IResult HandleCreateMicrohack(HttpContext context, HackboxDbContext dbContext, CreateMicrohackRequest request)
    {
        var authResult = RequireTechlead(context);
        if (authResult != null) return authResult;

        if (string.IsNullOrWhiteSpace(request.MicrohackId))
        {
            return Results.BadRequest(new { error = "microhackId is required" });
        }

        var microhackId = request.MicrohackId.Trim();
        if (dbContext.Teams.Any(team => team.Name == microhackId))
        {
            return Results.Conflict(new { error = $"Microhack '{microhackId}' already exists" });
        }

        var (scheduleStartUtc, environmentProvisioningUtc, scheduleEndUtc, validationError) = ResolveSchedule(
            request.StartDate ?? request.ScheduleStart,
            request.EnvironmentProvisioningDate,
            request.EndDate ?? request.ScheduleEnd);
        if (validationError != null)
        {
            return Results.BadRequest(new { error = validationError });
        }

        var assignedTeams = NormalizeTeams(request.Teams);
        var teamValidationError = ValidateTeamsExist(dbContext, assignedTeams);
        if (teamValidationError != null)
        {
            return Results.BadRequest(new { error = teamValidationError });
        }

        var entity = new TeamEntity
        {
            Name = microhackId,
            IsMicrohack = true,
            Enabled = request.Enabled,
            ScheduleStartUtc = scheduleStartUtc,
            ScheduleEndUtc = scheduleEndUtc,
            EnvironmentProvisioningUtc = environmentProvisioningUtc,
            TimeZone = request.TimeZone,
            ContentPath = request.ContentPath,
            EnvironmentReference = request.EnvironmentReference
        };

        dbContext.Teams.Add(entity);
        dbContext.SaveChanges();

        AssignTeamsToMicrohack(dbContext, microhackId, assignedTeams);
        dbContext.SaveChanges();

        return Results.Created($"/api/admin/microhacks/{microhackId}", ToResponse(entity, assignedTeams));
    }

    private static IResult HandleUpdateMicrohack(string microhackId, HttpContext context, HackboxDbContext dbContext, UpdateMicrohackRequest request)
    {
        var authResult = RequireTechlead(context);
        if (authResult != null) return authResult;

        var entity = dbContext.Teams.FirstOrDefault(team => team.Name == microhackId && team.IsMicrohack);
        if (entity == null)
        {
            return Results.NotFound(new { error = $"Microhack '{microhackId}' not found" });
        }

        var (scheduleStartUtc, environmentProvisioningUtc, scheduleEndUtc, validationError) = ResolveSchedule(
            request.StartDate ?? request.ScheduleStart,
            request.EnvironmentProvisioningDate,
            request.EndDate ?? request.ScheduleEnd,
            entity.ScheduleStartUtc,
            entity.EnvironmentProvisioningUtc,
            entity.ScheduleEndUtc);
        if (validationError != null)
        {
            return Results.BadRequest(new { error = validationError });
        }

        entity.Enabled = request.Enabled;
        entity.ScheduleStartUtc = scheduleStartUtc;
        entity.ScheduleEndUtc = scheduleEndUtc;
        entity.EnvironmentProvisioningUtc = environmentProvisioningUtc;
        entity.TimeZone = request.TimeZone;
        entity.ContentPath = request.ContentPath;
        entity.EnvironmentReference = request.EnvironmentReference;

        if (request.Teams != null)
        {
            var assignedTeams = NormalizeTeams(request.Teams);
            var teamValidationError = ValidateTeamsExist(dbContext, assignedTeams);
            if (teamValidationError != null)
            {
                return Results.BadRequest(new { error = teamValidationError });
            }

            AssignTeamsToMicrohack(dbContext, microhackId, assignedTeams);
        }

        dbContext.SaveChanges();

        var teams = dbContext.Teams
            .AsNoTracking()
            .Where(team => !team.IsMicrohack && team.MicrohackId == microhackId)
            .OrderBy(team => team.Name)
            .Select(team => team.Name)
            .ToList();

        return Results.Ok(ToResponse(entity, teams));
    }

    private static IResult HandleSetMicrohackEnabled(string microhackId, bool enabled, HttpContext context, HackboxDbContext dbContext)
    {
        var authResult = RequireTechlead(context);
        if (authResult != null) return authResult;

        var entity = dbContext.Teams.FirstOrDefault(team => team.Name == microhackId && team.IsMicrohack);
        if (entity == null)
        {
            return Results.NotFound(new { error = $"Microhack '{microhackId}' not found" });
        }

        entity.Enabled = enabled;
        dbContext.SaveChanges();

        var teams = dbContext.Teams
            .AsNoTracking()
            .Where(team => !team.IsMicrohack && team.MicrohackId == microhackId)
            .OrderBy(team => team.Name)
            .Select(team => team.Name)
            .ToList();

        return Results.Ok(ToResponse(entity, teams));
    }

    private static object ToResponse(TeamEntity microhack, List<string> assignedTeams) => new
    {
        microhackId = microhack.Name,
        enabled = microhack.Enabled,
        startDate = microhack.ScheduleStartUtc,
        endDate = microhack.ScheduleEndUtc,
        environmentProvisioningDate = microhack.EnvironmentProvisioningUtc,
        scheduleStart = microhack.ScheduleStartUtc,
        scheduleEnd = microhack.ScheduleEndUtc,
        timeZone = microhack.TimeZone,
        contentPath = microhack.ContentPath,
        environmentReference = microhack.EnvironmentReference,
        teams = assignedTeams,
        teamCount = assignedTeams.Count
    };

    private static List<string> NormalizeTeams(IEnumerable<string>? teams)
    {
        if (teams == null)
        {
            return new List<string>();
        }

        return teams
            .Where(teamName => !string.IsNullOrWhiteSpace(teamName))
            .Select(teamName => teamName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(teamName => teamName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ValidateTeamsExist(HackboxDbContext dbContext, List<string> teamNames)
    {
        if (teamNames.Count == 0)
        {
            return null;
        }

        var existingTeamNames = dbContext.Teams
            .AsNoTracking()
            .Where(team => !team.IsMicrohack && teamNames.Contains(team.Name))
            .Select(team => team.Name)
            .ToList();

        var missing = teamNames.Except(existingTeamNames, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count == 0)
        {
            return null;
        }

        return $"Unknown team(s): {string.Join(", ", missing)}";
    }

    private static void AssignTeamsToMicrohack(HackboxDbContext dbContext, string microhackId, List<string> teamNames)
    {
        if (teamNames.Count == 0)
        {
            return;
        }

        var selectedRows = dbContext.Teams
            .Where(team => !team.IsMicrohack && teamNames.Contains(team.Name))
            .ToList();

        foreach (var selected in selectedRows)
        {
            selected.MicrohackId = microhackId;
        }
    }

    private static (DateTime ScheduleStartUtc, DateTime EnvironmentProvisioningUtc, DateTime ScheduleEndUtc, string? ValidationError)
        ResolveSchedule(
            DateTime? scheduleStart,
            DateTime? environmentProvisioningDate,
            DateTime? scheduleEnd,
            DateTime? currentScheduleStart = null,
            DateTime? currentEnvironmentProvisioningDate = null,
            DateTime? currentScheduleEnd = null)
    {
        var scheduleStartUtc = ConvertToUtc(scheduleStart ?? currentScheduleStart ?? DateTime.UtcNow);
        var environmentProvisioningUtc = ConvertToUtc(
            environmentProvisioningDate
            ?? currentEnvironmentProvisioningDate
            ?? scheduleStartUtc.AddDays(-1));
        var scheduleEndUtc = ConvertToUtc(
            scheduleEnd
            ?? currentScheduleEnd
            ?? scheduleStartUtc.Date.AddDays(1));

        if (scheduleEndUtc <= scheduleStartUtc)
        {
            return (scheduleStartUtc, environmentProvisioningUtc, scheduleEndUtc, "endDate must be later than startDate");
        }

        if (environmentProvisioningUtc > scheduleStartUtc)
        {
            return (scheduleStartUtc, environmentProvisioningUtc, scheduleEndUtc, "environmentProvisioningDate must be on or before startDate");
        }

        return (scheduleStartUtc, environmentProvisioningUtc, scheduleEndUtc, null);
    }

    private static DateTime ConvertToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static IResult? RequireTechlead(HttpContext context)
    {
        if (context.Items["User"] is not AuthSession session)
        {
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);
        }

        if (session.Role != "techlead")
        {
            return Results.Json(new { error = "Forbidden — techlead role required" }, statusCode: 403);
        }

        return null;
    }
}

public class CreateMicrohackRequest
{
    public string MicrohackId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? EnvironmentProvisioningDate { get; set; }
    public DateTime? ScheduleStart { get; set; }
    public DateTime? ScheduleEnd { get; set; }
    public string? TimeZone { get; set; }
    public string? ContentPath { get; set; }
    public string? EnvironmentReference { get; set; }
    public List<string>? Teams { get; set; }
}

public class UpdateMicrohackRequest
{
    public bool Enabled { get; set; } = true;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? EnvironmentProvisioningDate { get; set; }
    public DateTime? ScheduleStart { get; set; }
    public DateTime? ScheduleEnd { get; set; }
    public string? TimeZone { get; set; }
    public string? ContentPath { get; set; }
    public string? EnvironmentReference { get; set; }
    public List<string>? Teams { get; set; }
}
