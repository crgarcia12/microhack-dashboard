using System.Text.Json;
using Api.Data.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.EfCore;

public class EfHackConfigRepository : IHackConfigRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfHackConfigRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public HackConfig GetConfig()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.HackConfig.AsNoTracking().FirstOrDefault(e => e.Id == 1);
        if (entity == null || string.IsNullOrEmpty(entity.ConfigJson))
        {
            return new HackConfig();
        }
        var config = JsonSerializer.Deserialize<HackConfig>(entity.ConfigJson) ?? new HackConfig();
        config.ContentPath = entity.ContentPath;
        return config;
    }

    public void SaveConfig(HackConfig config)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.HackConfig.FirstOrDefault(e => e.Id == 1);
        if (entity == null)
        {
            entity = new HackConfigEntity { Id = 1 };
            db.HackConfig.Add(entity);
        }
        entity.ContentPath = config.ContentPath;
        entity.ConfigJson = JsonSerializer.Serialize(config);
        entity.UpdatedAt = config.UpdatedAt;
        db.SaveChanges();
    }
}
