using System.Collections.Concurrent;
using System.Text.Json;
using Api.Models;

namespace Api.Data.File;

public class FileProgressRepository : IProgressRepository
{
    private readonly ConcurrentDictionary<string, TeamProgress> _cache = new();
    private readonly string _progressDir;
    private readonly ILogger<FileProgressRepository> _logger;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public FileProgressRepository(string progressDir, ILogger<FileProgressRepository> logger)
    {
        _progressDir = progressDir;
        _logger = logger;
        LoadProgressFiles();
    }

    public TeamProgress? GetProgress(string teamId)
    {
        _cache.TryGetValue(teamId, out var progress);
        return progress;
    }

    public IReadOnlyDictionary<string, TeamProgress> GetAllProgress()
    {
        return _cache;
    }

    public void SaveProgress(TeamProgress progress)
    {
        _cache[progress.TeamId] = progress;
        PersistProgress(progress);
    }

    private void LoadProgressFiles()
    {
        if (!Directory.Exists(_progressDir)) return;

        foreach (var file in Directory.GetFiles(_progressDir, "*.json"))
        {
            try
            {
                var json = System.IO.File.ReadAllText(file);
                var progress = JsonSerializer.Deserialize<TeamProgress>(json, ReadOptions);
                if (progress != null && !string.IsNullOrEmpty(progress.TeamId))
                {
                    _cache[progress.TeamId] = progress;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Corrupted progress file: {File}, treating as step 1", file);
            }
        }
    }

    private void PersistProgress(TeamProgress progress)
    {
        try
        {
            Directory.CreateDirectory(_progressDir);
            var filePath = Path.Combine(_progressDir, $"{progress.TeamId}.json");
            var json = JsonSerializer.Serialize(progress, WriteOptions);
            System.IO.File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist progress for team {TeamId}", progress.TeamId);
            throw;
        }
    }
}
