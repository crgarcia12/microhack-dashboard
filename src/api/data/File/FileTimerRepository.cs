using System.Collections.Concurrent;
using System.Text.Json;
using Api.Models;

namespace Api.Data.File;

public class FileTimerRepository : ITimerRepository
{
    private readonly ConcurrentDictionary<string, TimerState> _cache = new();
    private readonly string _dataDir;
    private readonly ILogger<FileTimerRepository> _logger;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FileTimerRepository(string dataDir, ILogger<FileTimerRepository> logger)
    {
        _dataDir = dataDir;
        _logger = logger;
        LoadTimerStates();
    }

    public TimerState GetTimerState(string teamName)
    {
        return _cache.GetOrAdd(teamName, name => new TimerState { TeamName = name });
    }

    public IReadOnlyList<TimerState> GetAllTimerStates()
    {
        return _cache.Values.ToList().AsReadOnly();
    }

    public void SaveTimerState(TimerState state)
    {
        _cache[state.TeamName] = state;
        Persist();
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(_dataDir);
            var filePath = Path.Combine(_dataDir, "timers.json");
            var allStates = _cache.Values.ToList();
            var json = JsonSerializer.Serialize(allStates, WriteOptions);
            System.IO.File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist timer states");
            throw;
        }
    }

    private void LoadTimerStates()
    {
        var filePath = Path.Combine(_dataDir, "timers.json");
        if (!System.IO.File.Exists(filePath)) return;

        try
        {
            var json = System.IO.File.ReadAllText(filePath);
            var states = JsonSerializer.Deserialize<List<TimerState>>(json, ReadOptions);
            if (states != null)
            {
                foreach (var state in states)
                {
                    if (!string.IsNullOrEmpty(state.TeamName))
                        _cache[state.TeamName] = state;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load timer states from {Path}", filePath);
        }
    }
}
