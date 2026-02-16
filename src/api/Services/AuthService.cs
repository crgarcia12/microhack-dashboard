using System.Collections.Concurrent;
using System.Text.Json;
using Api.Data;
using Api.Models;

namespace Api.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;

    public AuthService(IUserRepository userRepository, ISessionRepository sessionRepository)
    {
        _userRepository = userRepository;
        _sessionRepository = sessionRepository;
    }

    /// <summary>
    /// Constructor for tests that inject users directly (no DB needed).
    /// </summary>
    public AuthService(List<User> users, ISessionRepository? sessionRepository = null)
    {
        ValidateUsers(users);
        _userRepository = new InMemoryUserRepository(users);
        _sessionRepository = sessionRepository ?? new Data.File.FileSessionRepository();
    }

    public static void ValidateUsers(List<User> users)
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
        var user = _userRepository.GetUser(username);
        if (user == null) return null;

        // Case-sensitive password comparison
        if (user.Password != password) return null;

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
        return _userRepository.GetAllTeams();
    }

    /// <summary>
    /// Seeds the DB from the users.json file if the database has no users yet.
    /// </summary>
    public static void SeedFromFileIfEmpty(IUserRepository userRepository, string usersFilePath)
    {
        if (userRepository.HasUsers()) return;

        if (!File.Exists(usersFilePath)) return;

        var json = File.ReadAllText(usersFilePath);
        var config = JsonSerializer.Deserialize<UserConfig>(json);
        if (config?.Users == null || config.Users.Count == 0) return;

        ValidateUsers(config.Users);
        userRepository.SeedUsers(config.Users);
    }
}

/// <summary>
/// In-memory user repository for tests.
/// </summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users;

    public InMemoryUserRepository(List<User> users) => _users = users;

    public List<User> GetAllUsers() => _users.ToList();

    public User? GetUser(string username) =>
        _users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

    public void AddUser(User user) => _users.Add(user);

    public void UpdateUser(User user)
    {
        var idx = _users.FindIndex(u => string.Equals(u.Username, user.Username, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) _users[idx] = user;
    }

    public void DeleteUser(string username) =>
        _users.RemoveAll(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

    public List<string> GetAllTeams() =>
        _users.Where(u => !string.IsNullOrEmpty(u.Team))
              .Select(u => u.Team!).Distinct(StringComparer.OrdinalIgnoreCase)
              .OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();

    public void AddTeam(string teamName) { }
    public void DeleteTeam(string teamName) { }
    public bool HasUsers() => _users.Count > 0;
    public void SeedUsers(List<User> users) => _users.AddRange(users);
}
