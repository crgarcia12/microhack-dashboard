using Api.Data.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.EfCore;

public class EfTimerRepository : ITimerRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfTimerRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public TimerState GetTimerState(string teamName)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.TimerStates
            .Include(e => e.ChallengeTimes)
            .AsNoTracking()
            .FirstOrDefault(e => e.TeamName == teamName);

        return entity == null
            ? new TimerState { TeamName = teamName }
            : ToModel(entity);
    }

    public IReadOnlyList<TimerState> GetAllTimerStates()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        return db.TimerStates
            .Include(e => e.ChallengeTimes)
            .AsNoTracking()
            .Select(e => ToModel(e))
            .ToList()
            .AsReadOnly();
    }

    public void SaveTimerState(TimerState state)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
        var entity = db.TimerStates
            .Include(e => e.ChallengeTimes)
            .FirstOrDefault(e => e.TeamName == state.TeamName);

        if (entity == null)
        {
            entity = new TimerStateEntity { TeamName = state.TeamName };
            db.TimerStates.Add(entity);
        }

        entity.ManualTimerStatus = state.ManualTimer.Status;
        entity.ManualTimerStartedAt = state.ManualTimer.StartedAt;
        entity.ManualTimerAccumulatedSeconds = state.ManualTimer.AccumulatedSeconds;
        entity.TimerStartedAt = state.TimerStartedAt;

        // Sync challenge times
        var existingTimes = entity.ChallengeTimes.ToDictionary(ct => ct.ChallengeNumber);
        var desiredKeys = state.ChallengeTimes.Keys.Select(int.Parse).ToHashSet();

        // Remove old
        foreach (var ct in entity.ChallengeTimes.Where(ct => !desiredKeys.Contains(ct.ChallengeNumber)).ToList())
        {
            db.ChallengeTimes.Remove(ct);
            entity.ChallengeTimes.Remove(ct);
        }

        // Upsert
        foreach (var kvp in state.ChallengeTimes)
        {
            var num = int.Parse(kvp.Key);
            if (existingTimes.TryGetValue(num, out var existing))
            {
                existing.Seconds = kvp.Value;
            }
            else
            {
                entity.ChallengeTimes.Add(new ChallengeTimeEntity
                {
                    TeamName = state.TeamName,
                    ChallengeNumber = num,
                    Seconds = kvp.Value
                });
            }
        }

        db.SaveChanges();
    }

    private static TimerState ToModel(TimerStateEntity entity) => new()
    {
        TeamName = entity.TeamName,
        ManualTimer = new ManualTimerState
        {
            Status = entity.ManualTimerStatus,
            StartedAt = entity.ManualTimerStartedAt,
            AccumulatedSeconds = entity.ManualTimerAccumulatedSeconds
        },
        TimerStartedAt = entity.TimerStartedAt,
        ChallengeTimes = entity.ChallengeTimes
            .ToDictionary(ct => ct.ChallengeNumber.ToString(), ct => ct.Seconds)
    };
}
