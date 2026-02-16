using System.Text.Json;
using Api.Data;
using Api.Models;
using Microsoft.Extensions.Logging;

namespace Api.Services;

public class CredentialService : ICredentialService
{
    private readonly ICredentialRepository? _credentialRepository;
    private readonly Dictionary<string, TeamCredentials> _teamCredentials = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<CredentialService> _logger;

    /// <summary>
    /// DB-backed constructor: credentials come from ICredentialRepository.
    /// </summary>
    public CredentialService(ICredentialRepository credentialRepository, ILogger<CredentialService> logger)
    {
        _credentialRepository = credentialRepository;
        _logger = logger;
    }

    /// <summary>
    /// File-based constructor: credentials loaded from a JSON file (legacy).
    /// </summary>
    public CredentialService(string credentialsFilePath, ILogger<CredentialService> logger)
    {
        _logger = logger;
        LoadCredentials(credentialsFilePath);
    }

    private void LoadCredentials(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Credentials file not found at {Path}. All teams will see empty credentials.", filePath);
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read credentials file at {Path}.", filePath);
            return;
        }

        CredentialsFile? config;
        try
        {
            config = JsonSerializer.Deserialize<CredentialsFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse credentials file. All teams will see empty credentials.");
            return;
        }

        if (config?.Teams == null)
        {
            return;
        }

        foreach (var team in config.Teams)
        {
            // Filter out categories with empty credentials arrays (CRED edge case)
            var nonEmptyCategories = team.Categories
                .Where(c => c.Credentials.Count > 0)
                .ToList();

            _teamCredentials[team.TeamName] = new TeamCredentials
            {
                TeamName = team.TeamName,
                Categories = nonEmptyCategories
            };
        }
    }

    public TeamCredentials GetCredentials(string teamName)
    {
        if (_credentialRepository != null)
        {
            return _credentialRepository.GetCredentials(teamName);
        }

        if (_teamCredentials.TryGetValue(teamName, out var credentials))
        {
            return credentials;
        }

        // Team not found in credentials file — return empty (CRED-015)
        return new TeamCredentials
        {
            TeamName = teamName,
            Categories = new List<CredentialCategory>()
        };
    }

    /// <summary>
    /// Seeds the DB from the credentials.json file if the database has no credentials yet.
    /// </summary>
    public static void SeedFromFileIfEmpty(ICredentialRepository credentialRepository, string credentialsFilePath)
    {
        if (credentialRepository.HasCredentials()) return;
        if (!File.Exists(credentialsFilePath)) return;

        try
        {
            var json = File.ReadAllText(credentialsFilePath);
            var config = JsonSerializer.Deserialize<CredentialsFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config?.Teams == null || config.Teams.Count == 0) return;

            // Filter out empty categories
            foreach (var team in config.Teams)
            {
                team.Categories = team.Categories
                    .Where(c => c.Credentials.Count > 0)
                    .ToList();
            }

            credentialRepository.SeedCredentials(config.Teams);
        }
        catch (Exception)
        {
            // Silently skip seeding if file is malformed
        }
    }
}
