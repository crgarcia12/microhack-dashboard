using Api.Data.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.EfCore;

public class EfHackStateRepository : IHackStateRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfHackStateRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public HackState GetState()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.HackState.AsNoTracking().FirstOrDefault(e => e.Id == 1);
        if (entity == null)
        {
            return new HackState { Status = "not_started" };
        }
        return ToModel(entity);
    }

    public void UpdateState(HackState state)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.HackState.FirstOrDefault(e => e.Id == 1);
        if (entity == null)
        {
            entity = new HackStateEntity { Id = 1 };
            db.HackState.Add(entity);
        }
        entity.Status = state.Status;
        entity.StartedAt = state.StartedAt;
        entity.ConfiguredBy = state.ConfiguredBy;
        entity.UpdatedAt = state.UpdatedAt;
        db.SaveChanges();
    }

    private static HackState ToModel(HackStateEntity entity) => new()
    {
        Status = entity.Status,
        StartedAt = entity.StartedAt,
        ConfiguredBy = entity.ConfiguredBy,
        UpdatedAt = entity.UpdatedAt
    };
}
