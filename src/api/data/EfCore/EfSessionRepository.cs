using Api.Data.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.EfCore;

public class EfSessionRepository : ISessionRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfSessionRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public AuthSession? GetSession(string sessionId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.AuthSessions.AsNoTracking().FirstOrDefault(e => e.SessionId == sessionId);
        return entity == null ? null : ToModel(entity);
    }

    public void SaveSession(AuthSession session)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.AuthSessions.FirstOrDefault(e => e.SessionId == session.SessionId);
        if (entity == null)
        {
            entity = new AuthSessionEntity();
            db.AuthSessions.Add(entity);
        }
        entity.SessionId = session.SessionId;
        entity.Username = session.Username;
        entity.Role = session.Role;
        entity.Team = session.Team;
        entity.CreatedAt = session.CreatedAt;
        db.SaveChanges();
    }

    public void RemoveSession(string sessionId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.AuthSessions.FirstOrDefault(e => e.SessionId == sessionId);
        if (entity != null)
        {
            db.AuthSessions.Remove(entity);
            db.SaveChanges();
        }
    }

    public void RemoveSessionsByUsername(string username)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var sessions = db.AuthSessions
            .Where(e => e.Username.ToLower() == username.ToLower())
            .ToList();
        if (sessions.Count > 0)
        {
            db.AuthSessions.RemoveRange(sessions);
            db.SaveChanges();
        }
    }

    private static AuthSession ToModel(AuthSessionEntity entity) => new()
    {
        SessionId = entity.SessionId,
        Username = entity.Username,
        Role = entity.Role,
        Team = entity.Team,
        CreatedAt = entity.CreatedAt
    };
}
