using System.Collections.Concurrent;
using System.Text.Json;
using Api.Data;
using Api.Models;

namespace Api.Services;

public class AuthService : IAuthService
{
    private readonly List<User> _users;
    private readonly ISessionRepository _sessionRepository;

    public AuthService(string usersFilePath, ISessionRepository sessionRepository)
    {
        _users = LoadUsers(usersFilePath);
        _sessionRepository = sessionRepository;
    }

    public AuthService(List<User> users, ISessionRepository? sessionRepository = null)
    {
        ValidateUsers(users);
        _users = users;
        _sessionRepository = sessionRepository ?? new Data.File.FileSessionRepository();
    }

    private static List<User> LoadUsers(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"Users config file not found at {filePath}");
        }

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read users config: {ex.Message}", ex);
        }

        UserConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<UserConfig>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse users config: {ex.Message}", ex);
        }

        if (config == null)
        {
            throw new InvalidOperationException("Failed to parse users config: null result");
        }

        ValidateUsers(config.Users);
        return config.Users;
    }

    private static void ValidateUsers(List<User> users)
    {
        var validRoles = new HashSet<string> { "participant", "coach", "techlead" };
        var seenUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var user in users)
        {
            if (!seenUsernames.Add(user.Username))
            {
                throw new InvalidOperationException($"Duplicate username detected: {user.Username}");
            }

            if (!validRoles.Contains(user.Role))
            {
                throw new InvalidOperationException($"Invalid role '{user.Role}' for user '{user.Username}'");
            }

            if (user.Role is "participant" or "coach" && string.IsNullOrEmpty(user.Team))
            {
                throw new InvalidOperationException($"User '{user.Username}' with role '{user.Role}' must have a teamId");
            }

            if (user.Role == "techlead" && user.Team != null)
            {
                throw new InvalidOperationException($"User '{user.Username}' with role 'techlead' must not have a teamId");
            }
        }
    }

    public User? ValidateCredentials(string username, string password)
    {
        var user = _users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

        if (user == null)
            return null;

        // Case-sensitive password comparison
        if (user.Password != password)
            return null;

        return user;
    }

    public AuthSession CreateSession(User user)
    {
        // Invalidate previous session for same user (AUTH-010)
        _sessionRepository.RemoveSessionsByUsername(user.Username);

        var sessionId = Guid.NewGuid().ToString("N"); // 32 hex chars
        var session = new AuthSession
        {
            SessionId = sessionId,
            Username = user.Username,
            Role = user.Role,
            Team = user.Team,
            CreatedAt = DateTime.UtcNow
        };

        _sessionRepository.SaveSession(session);

        return session;
    }

    public AuthSession? GetSession(string sessionId)
    {
        return _sessionRepository.GetSession(sessionId);
    }

    public void RemoveSession(string sessionId)
    {
        _sessionRepository.RemoveSession(sessionId);
    }

    public IReadOnlyList<string> GetAllTeams()
    {
        return _users
            .Where(u => !string.IsNullOrEmpty(u.Team))
            .Select(u => u.Team!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
