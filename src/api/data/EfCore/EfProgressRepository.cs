using Api.Data.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.EfCore;

public class EfProgressRepository : IProgressRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfProgressRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public TeamProgress? GetProgress(string teamId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.TeamProgress.AsNoTracking().FirstOrDefault(e => e.TeamId == teamId);
        return entity == null ? null : ToModel(entity);
    }

    public IReadOnlyDictionary<string, TeamProgress> GetAllProgress()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        return db.TeamProgress.AsNoTracking()
            .ToDictionary(e => e.TeamId, e => ToModel(e));
    }

    public void SaveProgress(TeamProgress progress)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.TeamProgress.FirstOrDefault(e => e.TeamId == progress.TeamId);
        if (entity == null)
        {
            entity = new TeamProgressEntity();
            db.TeamProgress.Add(entity);
        }
        entity.TeamId = progress.TeamId;
        entity.CurrentStep = progress.CurrentStep;
        entity.UpdatedAt = progress.UpdatedAt;
        db.SaveChanges();
    }

    private static TeamProgress ToModel(TeamProgressEntity entity) => new()
    {
        TeamId = entity.TeamId,
        CurrentStep = entity.CurrentStep,
        UpdatedAt = entity.UpdatedAt
    };
}
