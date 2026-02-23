using Api.Data;
using Api.Models;

namespace Api.Services;

public static class HackModeHelper
{
    public const string TeamMode = "team";
    public const string IndividualMode = "individual";

    public static string NormalizeMode(string? mode)
    {
        return string.Equals(mode, IndividualMode, StringComparison.OrdinalIgnoreCase)
            ? IndividualMode
            : TeamMode;
    }

    public static bool IsIndividualMode(HackConfig config)
    {
        return string.Equals(NormalizeMode(config.Mode), IndividualMode, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsParticipantSolutionsVisible(HackConfig config)
    {
        return config.ParticipantSolutionsVisible ?? IsIndividualMode(config);
    }

    public static void ApplyDefaults(HackConfig config)
    {
        config.Mode = NormalizeMode(config.Mode);
        config.ParticipantSolutionsVisible ??= IsIndividualMode(config);
    }

    public static string ResolveProgressScope(AuthSession session, HackConfig config)
    {
        if (IsIndividualMode(config) && session.Role == "participant")
        {
            return session.Username;
        }

        if (!string.IsNullOrWhiteSpace(session.Team))
        {
            return session.Team;
        }

        return session.Username;
    }

    public static IReadOnlyList<string> GetDashboardScopes(HackConfig config, IAuthService authService, IUserRepository userRepository)
    {
        if (!IsIndividualMode(config))
        {
            return authService.GetAllTeams();
        }

        return userRepository.GetAllUsers()
            .Where(user => user.Role == "participant")
            .Select(user => user.Username)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(username => username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
