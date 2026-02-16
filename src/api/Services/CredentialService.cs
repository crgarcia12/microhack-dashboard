using System.Text.Json;
using Api.Models;
using Microsoft.Extensions.Logging;

namespace Api.Services;

public class CredentialService : ICredentialService
{
    private readonly Dictionary<string, TeamCredentials> _teamCredentials = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<CredentialService> _logger;

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
}
