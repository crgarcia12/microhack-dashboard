using System.Text.Json;
using Api.Models;

namespace Api.Data.File;

public class FileHackConfigRepository : IHackConfigRepository
{
    private readonly string _configFilePath;
    private readonly ILogger<FileHackConfigRepository> _logger;
    private HackConfig? _cachedConfig;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public FileHackConfigRepository(string configDir, ILogger<FileHackConfigRepository> logger)
    {
        _configFilePath = Path.Combine(configDir, "hack-config.json");
        _logger = logger;
        LoadConfig();
    }

    public HackConfig GetConfig()
    {
        return _cachedConfig ?? new HackConfig();
    }

    public void SaveConfig(HackConfig config)
    {
        _cachedConfig = config;
        PersistConfig(config);
    }

    private void LoadConfig()
    {
        if (!System.IO.File.Exists(_configFilePath))
        {
            _cachedConfig = new HackConfig();
            return;
        }

        try
        {
            var json = System.IO.File.ReadAllText(_configFilePath);
            _cachedConfig = JsonSerializer.Deserialize<HackConfig>(json, ReadOptions) ?? new HackConfig();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Corrupted hack config file, resetting to empty");
            _cachedConfig = new HackConfig();
        }
    }

    private void PersistConfig(HackConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(config, WriteOptions);
            System.IO.File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist hack config");
            throw;
        }
    }
}
