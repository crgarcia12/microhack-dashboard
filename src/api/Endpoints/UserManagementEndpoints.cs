using Api.Data;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Api.Endpoints;

public static class UserManagementEndpoints
{
    public static void MapUserManagementEndpoints(this WebApplication app)
    {
        // ── Teams ────────────────────────────────────────────────────────
        app.MapGet("/api/admin/team-admin/teams", HandleGetTeams)
           .WithName("TeamAdminGetTeams")
           .WithTags("UserManagement", "Admin");

        app.MapPost("/api/admin/team-admin/teams", HandleCreateTeam)
           .WithName("TeamAdminCreateTeam")
           .WithTags("UserManagement", "Admin");

        app.MapDelete("/api/admin/team-admin/teams/{teamName}", HandleDeleteTeam)
           .WithName("TeamAdminDeleteTeam")
           .WithTags("UserManagement", "Admin");

        app.MapPost("/api/admin/team-admin/teams/import-csv", HandleImportTeamsCsv)
           .WithName("TeamAdminImportTeamsCsv")
           .WithTags("UserManagement", "Admin")
           .DisableAntiforgery();

        app.MapGet("/api/admin/team-admin/teams/export-csv", HandleExportTeamsCsv)
           .WithName("TeamAdminExportTeamsCsv")
           .WithTags("UserManagement", "Admin");

        // ── Users (hackers, coaches, techleads) ─────────────────────────
        app.MapGet("/api/admin/team-admin/users", HandleGetUsers)
           .WithName("TeamAdminGetUsers")
           .WithTags("UserManagement", "Admin");

        app.MapPost("/api/admin/team-admin/users", HandleCreateUser)
           .WithName("TeamAdminCreateUser")
           .WithTags("UserManagement", "Admin");

        app.MapPut("/api/admin/team-admin/users/{username}", HandleUpdateUser)
           .WithName("TeamAdminUpdateUser")
           .WithTags("UserManagement", "Admin");

        app.MapDelete("/api/admin/team-admin/users/{username}", HandleDeleteUser)
           .WithName("TeamAdminDeleteUser")
           .WithTags("UserManagement", "Admin");

        app.MapPost("/api/admin/team-admin/users/import-csv", HandleImportUsersCsv)
           .WithName("TeamAdminImportUsersCsv")
           .WithTags("UserManagement", "Admin")
           .DisableAntiforgery();

        app.MapGet("/api/admin/team-admin/users/export-csv", HandleExportUsersCsv)
           .WithName("TeamAdminExportUsersCsv")
           .WithTags("UserManagement", "Admin");
    }

    // ── Teams ────────────────────────────────────────────────────────────

    private static IResult HandleGetTeams(HttpContext context, IUserRepository userRepo)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var teams = userRepo.GetAllTeams();
        return Results.Ok(teams);
    }

    private static IResult HandleCreateTeam(HttpContext context, IUserRepository userRepo, HackboxDbContext dbContext, CreateTeamRequest body)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var teamName = body.Name?.Trim() ?? string.Empty;
        var microhackId = body.MicrohackId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(teamName))
            return Results.BadRequest(new { error = "Team name is required" });

        if (string.IsNullOrWhiteSpace(microhackId))
        {
            microhackId = ResolveDefaultMicrohackId(dbContext) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(microhackId))
            return Results.BadRequest(new { error = "MicrohackId is required because no microhack exists" });

        var microhackExists = dbContext.Teams
            .AsNoTracking()
            .Any(t => t.IsMicrohack && t.Name == microhackId);
        if (!microhackExists)
            return Results.BadRequest(new { error = $"Microhack '{microhackId}' was not found" });

        if (userRepo.GetAllTeams().Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.Conflict(new { error = $"Team '{teamName}' already exists" });

        userRepo.AddTeam(teamName, microhackId);
        return Results.Created($"/api/admin/team-admin/teams/{teamName}", new { name = teamName, microhackId });
    }

    private static IResult HandleDeleteTeam(string teamName, HttpContext context, IUserRepository userRepo)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        if (!userRepo.GetAllTeams().Contains(teamName, StringComparer.OrdinalIgnoreCase))
            return Results.NotFound(new { error = $"Team '{teamName}' not found" });

        // Check if any users still belong to this team
        var usersInTeam = userRepo.GetAllUsers().Where(u => string.Equals(u.Team, teamName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (usersInTeam.Count > 0)
            return Results.Conflict(new { error = $"Cannot delete team '{teamName}' — it has {usersInTeam.Count} user(s)" });

        userRepo.DeleteTeam(teamName);
        return Results.Ok(new { deleted = teamName });
    }

    private static async Task<IResult> HandleImportTeamsCsv(HttpContext context, IUserRepository userRepo, HackboxDbContext dbContext, IFormFile? file)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var csv = await ParseCsvRows(file);
        if (csv.Error != null)
            return Results.BadRequest(new { error = csv.Error });

        var teamColumn = csv.Headers.FirstOrDefault(h =>
            h.Equals("name", StringComparison.OrdinalIgnoreCase)
            || h.Equals("team", StringComparison.OrdinalIgnoreCase));
        if (teamColumn == null)
            return Results.BadRequest(new { error = "CSV must include a 'name' or 'team' column" });

        var microhackColumn = csv.Headers.FirstOrDefault(h =>
            h.Equals("microhack", StringComparison.OrdinalIgnoreCase)
            || h.Equals("microhackId", StringComparison.OrdinalIgnoreCase));

        var defaultMicrohackId = ResolveDefaultMicrohackId(dbContext);
        if (microhackColumn == null && string.IsNullOrWhiteSpace(defaultMicrohackId))
            return Results.BadRequest(new { error = "CSV must include a 'microhack' or 'microhackId' column because no microhack exists" });

        var availableMicrohacks = dbContext.Teams
            .AsNoTracking()
            .Where(t => t.IsMicrohack)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var teams = new List<(string TeamName, string MicrohackId)>();
        var seenTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<object>();

        foreach (var row in csv.Rows)
        {
            var teamName = row.Values.GetValueOrDefault(teamColumn)?.Trim() ?? string.Empty;
            var microhackId = microhackColumn == null
                ? defaultMicrohackId ?? string.Empty
                : row.Values.GetValueOrDefault(microhackColumn)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(teamName))
            {
                errors.Add(new { row = row.RowNumber, error = "Team name is required" });
                continue;
            }

            if (string.IsNullOrWhiteSpace(microhackId))
            {
                errors.Add(new { row = row.RowNumber, error = "MicrohackId is required" });
                continue;
            }

            if (!availableMicrohacks.Contains(microhackId))
            {
                errors.Add(new { row = row.RowNumber, error = $"Microhack '{microhackId}' not found" });
                continue;
            }

            if (!seenTeams.Add(teamName))
            {
                errors.Add(new { row = row.RowNumber, error = $"Duplicate team '{teamName}' in CSV" });
                continue;
            }

            teams.Add((teamName, microhackId));
        }

        if (errors.Count > 0)
            return Results.BadRequest(new { error = "CSV import validation failed", details = errors });

        var existingTeams = new HashSet<string>(userRepo.GetAllTeams(), StringComparer.OrdinalIgnoreCase);
        var created = 0;
        var updated = 0;

        foreach (var (teamName, microhackId) in teams)
        {
            if (existingTeams.Contains(teamName))
            {
                updated++;
                continue;
            }

            userRepo.AddTeam(teamName, microhackId);
            existingTeams.Add(teamName);
            created++;
        }

        return Results.Ok(new
        {
            imported = teams.Count,
            created,
            updated
        });
    }

    private static IResult HandleExportTeamsCsv(HttpContext context, IUserRepository userRepo)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("name");

        foreach (var team in userRepo.GetAllTeams())
        {
            csvBuilder.AppendLine(EscapeCsvValue(team));
        }

        return Results.File(Encoding.UTF8.GetBytes(csvBuilder.ToString()), "text/csv", "teams.csv");
    }

    // ── Users ────────────────────────────────────────────────────────────

    private static IResult HandleGetUsers(HttpContext context, IUserRepository userRepo, string? role = null)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var users = userRepo.GetAllUsers();

        if (!string.IsNullOrEmpty(role))
            users = users.Where(u => string.Equals(u.Role, role, StringComparison.OrdinalIgnoreCase)).ToList();

        // Don't expose passwords in the response
        var result = users.Select(u => new
        {
            u.Username,
            u.Role,
            u.Team
        });

        return Results.Ok(result);
    }

    private static IResult HandleCreateUser(HttpContext context, IUserRepository userRepo, IHackConfigRepository hackConfigRepository, HackboxDbContext dbContext, CreateUserRequest body)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var isIndividualMode = IsIndividualMode(hackConfigRepository);
        var team = ResolveUserTeam(body.Username, body.Role, body.Team, isIndividualMode);
        var validationError = ValidateUserRequest(body.Username, body.Password, body.Role, team);
        if (validationError != null)
            return Results.BadRequest(new { error = validationError });

        if (userRepo.GetUser(body.Username) != null)
            return Results.Conflict(new { error = $"User '{body.Username}' already exists" });

        // Ensure team exists for participant/coach
        if (!string.IsNullOrEmpty(team) && !userRepo.GetAllTeams().Contains(team, StringComparer.OrdinalIgnoreCase))
        {
            var defaultMicrohackId = ResolveDefaultMicrohackId(dbContext);
            if (string.IsNullOrWhiteSpace(defaultMicrohackId))
                return Results.BadRequest(new { error = $"Team '{team}' does not exist and no microhack is available for auto-creation" });

            userRepo.AddTeam(team, defaultMicrohackId);
        }

        userRepo.AddUser(new User
        {
            Username = body.Username,
            Password = body.Password,
            Role = body.Role,
            Team = team
        });

        return Results.Created($"/api/admin/team-admin/users/{body.Username}", new
        {
            body.Username,
            body.Role,
            Team = team
        });
    }

    private static IResult HandleUpdateUser(string username, HttpContext context, IUserRepository userRepo, IHackConfigRepository hackConfigRepository, HackboxDbContext dbContext, UpdateUserRequest body)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var existing = userRepo.GetUser(username);
        if (existing == null)
            return Results.NotFound(new { error = $"User '{username}' not found" });

        var isIndividualMode = IsIndividualMode(hackConfigRepository);
        var role = body.Role ?? existing.Role;
        var team = ResolveUserTeam(username, role, body.Team ?? existing.Team, isIndividualMode);
        var password = body.Password ?? existing.Password;

        var validationError = ValidateUserRequest(username, password, role, team);
        if (validationError != null)
            return Results.BadRequest(new { error = validationError });

        if (!string.IsNullOrEmpty(team) && !userRepo.GetAllTeams().Contains(team, StringComparer.OrdinalIgnoreCase))
        {
            var defaultMicrohackId = ResolveDefaultMicrohackId(dbContext);
            if (string.IsNullOrWhiteSpace(defaultMicrohackId))
                return Results.BadRequest(new { error = $"Team '{team}' does not exist and no microhack is available for auto-creation" });

            userRepo.AddTeam(team, defaultMicrohackId);
        }

        userRepo.UpdateUser(new User
        {
            Username = username,
            Password = password,
            Role = role,
            Team = team
        });

        return Results.Ok(new { Username = username, Role = role, Team = team });
    }

    private static IResult HandleDeleteUser(string username, HttpContext context, IUserRepository userRepo)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        if (userRepo.GetUser(username) == null)
            return Results.NotFound(new { error = $"User '{username}' not found" });

        userRepo.DeleteUser(username);
        return Results.Ok(new { deleted = username });
    }

    private static async Task<IResult> HandleImportUsersCsv(HttpContext context, IUserRepository userRepo, IHackConfigRepository hackConfigRepository, HackboxDbContext dbContext, IFormFile? file)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;
        var isIndividualMode = IsIndividualMode(hackConfigRepository);

        var csv = await ParseCsvRows(file);
        if (csv.Error != null)
            return Results.BadRequest(new { error = csv.Error });

        if (!csv.Headers.Any(h => h.Equals("username", StringComparison.OrdinalIgnoreCase)))
            return Results.BadRequest(new { error = "CSV must include a 'username' column" });

        var existingUsersByUsername = userRepo.GetAllUsers()
            .ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase);
        var seenUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<PendingUserUpsert>();
        var errors = new List<object>();

        foreach (var row in csv.Rows)
        {
            var username = row.Values.GetValueOrDefault("username")?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                errors.Add(new { row = row.RowNumber, error = "Username is required" });
                continue;
            }

            if (!seenUsernames.Add(username))
            {
                errors.Add(new { row = row.RowNumber, error = $"Duplicate username '{username}' in CSV" });
                continue;
            }

            existingUsersByUsername.TryGetValue(username, out var existing);

            var role = row.Values.GetValueOrDefault("role")?.Trim();
            if (string.IsNullOrWhiteSpace(role))
                role = existing?.Role ?? string.Empty;

            var team = (row.Values.TryGetValue("team", out var csvTeam)
                || row.Values.TryGetValue("teamName", out csvTeam))
                ? string.IsNullOrWhiteSpace(csvTeam) ? null : csvTeam.Trim()
                : existing?.Team;

            var password = row.Values.GetValueOrDefault("password")?.Trim();
            if (string.IsNullOrWhiteSpace(password))
                password = existing?.Password ?? string.Empty;

            var persistedUsername = existing?.Username ?? username;
            team = ResolveUserTeam(persistedUsername, role!, team, isIndividualMode);
            var validationError = ValidateUserRequest(persistedUsername, password, role!, team);
            if (validationError != null)
            {
                errors.Add(new { row = row.RowNumber, error = validationError });
                continue;
            }

            pending.Add(new PendingUserUpsert(
                new User
                {
                    Username = persistedUsername,
                    Password = password,
                    Role = role!,
                    Team = team
                },
                existing == null));
        }

        if (errors.Count > 0)
            return Results.BadRequest(new { error = "CSV import validation failed", details = errors });

        var existingTeams = new HashSet<string>(userRepo.GetAllTeams(), StringComparer.OrdinalIgnoreCase);
        var requiredTeams = pending
            .Select(operation => operation.User.Team)
            .Where(team => !string.IsNullOrWhiteSpace(team))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(team => team!)
            .ToList();

        var teamsCreated = 0;
        var defaultMicrohackId = ResolveDefaultMicrohackId(dbContext);
        if (requiredTeams.Any(team => !existingTeams.Contains(team)) && string.IsNullOrWhiteSpace(defaultMicrohackId))
            return Results.BadRequest(new { error = "Cannot auto-create teams because no microhack exists" });

        foreach (var team in requiredTeams)
        {
            if (existingTeams.Contains(team))
                continue;

            userRepo.AddTeam(team, defaultMicrohackId!);
            existingTeams.Add(team);
            teamsCreated++;
        }

        var created = 0;
        var updated = 0;
        foreach (var operation in pending)
        {
            if (operation.IsCreate)
            {
                userRepo.AddUser(operation.User);
                created++;
            }
            else
            {
                userRepo.UpdateUser(operation.User);
                updated++;
            }
        }

        return Results.Ok(new
        {
            imported = pending.Count,
            created,
            updated,
            teamsCreated
        });
    }

    private static IResult HandleExportUsersCsv(HttpContext context, IUserRepository userRepo)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("username,role,team");

        foreach (var user in userRepo.GetAllUsers())
        {
            csvBuilder.AppendLine(string.Join(",",
                EscapeCsvValue(user.Username),
                EscapeCsvValue(user.Role),
                EscapeCsvValue(user.Team)));
        }

        return Results.File(Encoding.UTF8.GetBytes(csvBuilder.ToString()), "text/csv", "users.csv");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static async Task<(List<CsvRow> Rows, List<string> Headers, string? Error)> ParseCsvRows(IFormFile? file)
    {
        var emptyRows = new List<CsvRow>();
        var emptyHeaders = new List<string>();

        if (file == null || file.Length == 0)
            return (emptyRows, emptyHeaders, "CSV file is required");

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);

        List<string>? headers = null;
        var rows = new List<CsvRow>();
        var lineNumber = 0;

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!TryParseCsvLine(line, out var values, out var parseError))
                return (emptyRows, emptyHeaders, $"Invalid CSV format on line {lineNumber}: {parseError}");

            if (headers == null)
            {
                headers = values.Select(v => v.Trim().TrimStart('\uFEFF')).ToList();
                if (headers.Count == 0 || headers.Any(string.IsNullOrWhiteSpace))
                    return (emptyRows, emptyHeaders, "CSV header row is required");

                var duplicateHeader = headers
                    .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(group => group.Count() > 1);
                if (duplicateHeader != null)
                    return (emptyRows, emptyHeaders, $"CSV contains duplicate header '{duplicateHeader.Key}'");

                continue;
            }

            if (values.Count != headers.Count)
            {
                return (emptyRows, emptyHeaders,
                    $"CSV row {lineNumber} has {values.Count} column(s); expected {headers.Count}");
            }

            var valueMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                valueMap[headers[i]] = values[i].Trim();
            }

            rows.Add(new CsvRow(lineNumber, valueMap));
        }

        if (headers == null)
            return (emptyRows, emptyHeaders, "CSV header row is required");

        return (rows, headers, null);
    }

    private static bool TryParseCsvLine(string line, out List<string> values, out string? error)
    {
        values = new List<string>();
        error = null;

        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (character == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (inQuotes)
        {
            error = "Unterminated quoted field";
            return false;
        }

        values.Add(current.ToString());
        return true;
    }

    private static string EscapeCsvValue(string? value)
    {
        value ??= string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }

    private static bool IsIndividualMode(IHackConfigRepository hackConfigRepository)
    {
        var config = hackConfigRepository.GetConfig();
        return HackModeHelper.IsIndividualMode(config);
    }

    private static string? ResolveDefaultMicrohackId(HackboxDbContext dbContext)
    {
        return dbContext.Teams
            .AsNoTracking()
            .Where(t => t.IsMicrohack)
            .OrderBy(t => t.Name)
            .Select(t => t.Name)
            .FirstOrDefault();
    }

    private static string? ResolveUserTeam(string username, string role, string? team, bool isIndividualMode)
    {
        if (role == "techlead")
            return null;

        if (isIndividualMode)
            return username;

        return team;
    }

    private static string? ValidateUserRequest(string username, string password, string role, string? team)
    {
        if (string.IsNullOrWhiteSpace(username)) return "Username is required";
        if (string.IsNullOrWhiteSpace(password)) return "Password is required";

        var validRoles = new HashSet<string> { "participant", "coach", "techlead" };
        if (!validRoles.Contains(role)) return $"Invalid role '{role}'. Must be participant, coach, or techlead";

        if (role is "participant" or "coach" && string.IsNullOrEmpty(team))
            return $"Users with role '{role}' must have a team";

        return null;
    }

    private static IResult? RequireTechlead(HttpContext context)
    {
        var session = context.Items["User"] as AuthSession;
        if (session == null)
            return Results.Json(new { error = "Not authenticated" }, statusCode: 401);
        if (session.Role != "techlead")
            return Results.Json(new { error = "Forbidden — techlead role required" }, statusCode: 403);
        return null;
    }

    private sealed record CsvRow(int RowNumber, Dictionary<string, string> Values);
    private sealed record PendingUserUpsert(User User, bool IsCreate);
}

// ── Request DTOs ─────────────────────────────────────────────────────────

public class CreateTeamRequest
{
    public string Name { get; set; } = string.Empty;
    public string MicrohackId { get; set; } = string.Empty;
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Team { get; set; }
}

public class UpdateUserRequest
{
    public string? Password { get; set; }
    public string? Role { get; set; }
    public string? Team { get; set; }
}
