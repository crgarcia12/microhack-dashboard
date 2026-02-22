using Api.Data;
using Api.Models;
using Api.Services;

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
    }

    // ── Teams ────────────────────────────────────────────────────────────

    private static IResult HandleGetTeams(HttpContext context, IUserRepository userRepo)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var teams = userRepo.GetAllTeams();
        return Results.Ok(teams);
    }

    private static IResult HandleCreateTeam(HttpContext context, IUserRepository userRepo, CreateTeamRequest body)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        if (string.IsNullOrWhiteSpace(body.Name))
            return Results.BadRequest(new { error = "Team name is required" });

        if (userRepo.GetAllTeams().Contains(body.Name, StringComparer.OrdinalIgnoreCase))
            return Results.Conflict(new { error = $"Team '{body.Name}' already exists" });

        userRepo.AddTeam(body.Name);
        return Results.Created($"/api/admin/team-admin/teams/{body.Name}", new { name = body.Name });
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

    private static IResult HandleCreateUser(HttpContext context, IUserRepository userRepo, CreateUserRequest body)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var validationError = ValidateUserRequest(body.Username, body.Password, body.Role, body.Team);
        if (validationError != null)
            return Results.BadRequest(new { error = validationError });

        if (userRepo.GetUser(body.Username) != null)
            return Results.Conflict(new { error = $"User '{body.Username}' already exists" });

        // Ensure team exists for participant/coach
        if (!string.IsNullOrEmpty(body.Team) && !userRepo.GetAllTeams().Contains(body.Team, StringComparer.OrdinalIgnoreCase))
        {
            userRepo.AddTeam(body.Team);
        }

        userRepo.AddUser(new User
        {
            Username = body.Username,
            Password = body.Password,
            Role = body.Role,
            Team = body.Team
        });

        return Results.Created($"/api/admin/team-admin/users/{body.Username}", new
        {
            body.Username,
            body.Role,
            body.Team
        });
    }

    private static IResult HandleUpdateUser(string username, HttpContext context, IUserRepository userRepo, UpdateUserRequest body)
    {
        var auth = RequireTechlead(context);
        if (auth != null) return auth;

        var existing = userRepo.GetUser(username);
        if (existing == null)
            return Results.NotFound(new { error = $"User '{username}' not found" });

        var role = body.Role ?? existing.Role;
        var team = body.Team ?? existing.Team;
        var password = body.Password ?? existing.Password;

        var validationError = ValidateUserRequest(username, password, role, team);
        if (validationError != null)
            return Results.BadRequest(new { error = validationError });

        if (!string.IsNullOrEmpty(team) && !userRepo.GetAllTeams().Contains(team, StringComparer.OrdinalIgnoreCase))
        {
            userRepo.AddTeam(team);
        }

        userRepo.UpdateUser(new User
        {
            Username = username,
            Password = password,
            Role = role,
            Team = role == "techlead" ? null : team
        });

        return Results.Ok(new { Username = username, Role = role, Team = role == "techlead" ? null : team });
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

    // ── Helpers ──────────────────────────────────────────────────────────

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
}

// ── Request DTOs ─────────────────────────────────────────────────────────

public class CreateTeamRequest
{
    public string Name { get; set; } = string.Empty;
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
