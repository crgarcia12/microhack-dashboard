using System.Text.Json;
using Api.Models;

namespace Api.Data.File;

public class FileHackStateRepository : IHackStateRepository
{
    private readonly string _stateFilePath;
    private readonly ILogger<FileHackStateRepository> _logger;
    private HackState? _cachedState;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public FileHackStateRepository(string stateDir, ILogger<FileHackStateRepository> logger)
    {
        _stateFilePath = Path.Combine(stateDir, "hack-state.json");
        _logger = logger;
        LoadState();
    }

    public HackState GetState()
    {
        return _cachedState ?? new HackState { Status = "not_started" };
    }

    public void UpdateState(HackState state)
    {
        _cachedState = state;
        PersistState(state);
    }

    private void LoadState()
    {
        if (!System.IO.File.Exists(_stateFilePath))
        {
            _cachedState = new HackState { Status = "not_started" };
            return;
        }

        try
        {
            var json = System.IO.File.ReadAllText(_stateFilePath);
            _cachedState = JsonSerializer.Deserialize<HackState>(json, ReadOptions) ?? new HackState { Status = "not_started" };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Corrupted hack state file, resetting to not_started");
            _cachedState = new HackState { Status = "not_started" };
        }
    }

    private void PersistState(HackState state)
    {
        try
        {
            var dir = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(state, WriteOptions);
            System.IO.File.WriteAllText(_stateFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist hack state");
            throw;
        }
    }
}
