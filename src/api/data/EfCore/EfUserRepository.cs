using Api.Data.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.EfCore;

public class EfUserRepository : IUserRepository
{
    private readonly HackboxDbContext _db;

    public EfUserRepository(HackboxDbContext db)
    {
        _db = db;
    }

    public List<User> GetAllUsers()
    {
        return _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new User
            {
                Username = u.Username,
                Password = u.Password,
                Role = u.Role,
                Team = u.TeamName
            })
            .ToList();
    }

    public User? GetUser(string username)
    {
        var entity = _db.Users
            .AsNoTracking()
            .FirstOrDefault(u => u.Username.ToLower() == username.ToLower());

        if (entity == null) return null;

        return new User
        {
            Username = entity.Username,
            Password = entity.Password,
            Role = entity.Role,
            Team = entity.TeamName
        };
    }

    public void AddUser(User user)
    {
        _db.Users.Add(new UserEntity
        {
            Username = user.Username,
            Password = user.Password,
            Role = user.Role,
            TeamName = user.Team
        });
        _db.SaveChanges();
    }

    public void UpdateUser(User user)
    {
        var entity = _db.Users.Find(user.Username);
        if (entity == null) return;

        entity.Password = user.Password;
        entity.Role = user.Role;
        entity.TeamName = user.Team;
        _db.SaveChanges();
    }

    public void DeleteUser(string username)
    {
        var entity = _db.Users.Find(username);
        if (entity != null)
        {
            _db.Users.Remove(entity);
            _db.SaveChanges();
        }
    }

    public List<string> GetAllTeams()
    {
        return _db.Teams
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => t.Name)
            .ToList();
    }

    public void AddTeam(string teamName)
    {
        if (!_db.Teams.Any(t => t.Name == teamName))
        {
            _db.Teams.Add(new TeamEntity { Name = teamName });
            _db.SaveChanges();
        }
    }

    public void DeleteTeam(string teamName)
    {
        var entity = _db.Teams.Find(teamName);
        if (entity != null)
        {
            _db.Teams.Remove(entity);
            _db.SaveChanges();
        }
    }

    public bool HasUsers()
    {
        return _db.Users.Any();
    }

    public void SeedUsers(List<User> users)
    {
        // Seed teams first
        var teamNames = users
            .Where(u => !string.IsNullOrEmpty(u.Team))
            .Select(u => u.Team!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var teamName in teamNames)
        {
            if (!_db.Teams.Any(t => t.Name == teamName))
            {
                _db.Teams.Add(new TeamEntity { Name = teamName });
            }
        }
        _db.SaveChanges();

        // Seed users
        foreach (var user in users)
        {
            if (!_db.Users.Any(u => u.Username == user.Username))
            {
                _db.Users.Add(new UserEntity
                {
                    Username = user.Username,
                    Password = user.Password,
                    Role = user.Role,
                    TeamName = user.Team
                });
            }
        }
        _db.SaveChanges();
    }
}
