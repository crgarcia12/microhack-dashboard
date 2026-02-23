using Api.Data;
using Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Services;

public class HackStateService : IHackStateService
{
    private readonly IHackStateRepository _stateRepository;
    private readonly IHackConfigRepository _configRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProgressRepository _progressRepository;
    private readonly ITimerRepository _timerRepository;
    private readonly ILogger<HackStateService> _logger;

    public HackStateService(
        IHackStateRepository stateRepository,
        IHackConfigRepository configRepository,
        IServiceScopeFactory scopeFactory,
        IProgressRepository progressRepository,
        ITimerRepository timerRepository,
        ILogger<HackStateService> logger)
    {
        _stateRepository = stateRepository;
        _configRepository = configRepository;
        _scopeFactory = scopeFactory;
        _progressRepository = progressRepository;
        _timerRepository = timerRepository;
        _logger = logger;
    }

    public HackState GetState()
    {
        return _stateRepository.GetState();
    }

    public HackConfig GetConfig()
    {
        var config = _configRepository.GetConfig();
        HackModeHelper.ApplyDefaults(config);
        return config;
    }

    public void SaveConfig(HackConfig config, string configuredBy)
    {
        var previousConfig = GetConfig();
        var previousMode = HackModeHelper.NormalizeMode(previousConfig.Mode);

        HackModeHelper.ApplyDefaults(config);
        var nextMode = HackModeHelper.NormalizeMode(config.Mode);
        if (!string.Equals(previousMode, nextMode, StringComparison.OrdinalIgnoreCase))
        {
            MigrateModeData(previousMode, nextMode);
            _logger.LogInformation("Hack mode changed from {PreviousMode} to {NextMode}", previousMode, nextMode);
        }

        config.UpdatedAt = DateTime.UtcNow;
        _configRepository.SaveConfig(config);

        var state = _stateRepository.GetState();
        if (state.Status == "not_started")
        {
            state.Status = "waiting";
            state.ConfiguredBy = configuredBy;
            state.UpdatedAt = DateTime.UtcNow;
            _stateRepository.UpdateState(state);
            _logger.LogInformation("Hack configured by {User}, state changed to waiting", configuredBy);
        }
    }

    public bool LaunchHack(string launchedBy)
    {
        var state = _stateRepository.GetState();
        if (state.Status == "waiting" || state.Status == "configuration" || state.Status == "not_started")
        {
            state.Status = "active";
            state.StartedAt = DateTime.UtcNow;
            state.ConfiguredBy = launchedBy;
            state.UpdatedAt = DateTime.UtcNow;
            _stateRepository.UpdateState(state);
            _logger.LogInformation("Hack launched by {User} at {Time}", launchedBy, state.StartedAt);
            return true;
        }

        _logger.LogWarning("Cannot launch hack from status {Status}", state.Status);
        return false;
    }

    public bool PauseHack(string pausedBy)
    {
        var state = _stateRepository.GetState();
        if (state.Status == "active")
        {
            state.Status = "waiting";
            state.UpdatedAt = DateTime.UtcNow;
            state.ConfiguredBy = pausedBy;
            _stateRepository.UpdateState(state);
            _logger.LogInformation("Hack paused by {User}", pausedBy);
            return true;
        }

        _logger.LogWarning("Cannot pause hack from status {Status}", state.Status);
        return false;
    }

    public bool IsHackActive()
    {
        var state = _stateRepository.GetState();
        return state.Status == "active";
    }

    private void MigrateModeData(string previousMode, string nextMode)
    {
        using var scope = _scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var participants = userRepository.GetAllUsers()
            .Where(user => user.Role == "participant")
            .ToList();

        var progressByScope = _progressRepository.GetAllProgress()
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        var timersByScope = _timerRepository.GetAllTimerStates()
            .ToDictionary(timer => timer.TeamName, timer => timer, StringComparer.OrdinalIgnoreCase);

        if (previousMode == HackModeHelper.TeamMode && nextMode == HackModeHelper.IndividualMode)
        {
            foreach (var participant in participants)
            {
                var sourceScope = !string.IsNullOrWhiteSpace(participant.Team) ? participant.Team : participant.Username;
                var targetScope = participant.Username;

                if (!progressByScope.ContainsKey(targetScope) && progressByScope.TryGetValue(sourceScope, out var sourceProgress))
                {
                    _progressRepository.SaveProgress(CloneProgress(sourceProgress, targetScope));
                }

                if (!timersByScope.ContainsKey(targetScope) && timersByScope.TryGetValue(sourceScope, out var sourceTimer))
                {
                    _timerRepository.SaveTimerState(CloneTimer(sourceTimer, targetScope));
                }
            }

            return;
        }

        if (previousMode == HackModeHelper.IndividualMode && nextMode == HackModeHelper.TeamMode)
        {
            var participantGroups = participants
                .Where(participant => !string.IsNullOrWhiteSpace(participant.Team))
                .GroupBy(participant => participant.Team!, StringComparer.OrdinalIgnoreCase);

            foreach (var group in participantGroups)
            {
                var teamName = group.Key;

                if (!progressByScope.ContainsKey(teamName))
                {
                    var sourceProgress = group
                        .Select(participant => progressByScope.TryGetValue(participant.Username, out var value) ? value : null)
                        .Where(value => value != null)
                        .OrderByDescending(value => value!.CurrentStep)
                        .ThenByDescending(value => value!.UpdatedAt)
                        .FirstOrDefault();

                    if (sourceProgress != null)
                    {
                        _progressRepository.SaveProgress(CloneProgress(sourceProgress, teamName));
                    }
                }

                if (!timersByScope.ContainsKey(teamName))
                {
                    var sourceTimer = group
                        .Select(participant => timersByScope.TryGetValue(participant.Username, out var value) ? value : null)
                        .FirstOrDefault(value => value != null);

                    if (sourceTimer != null)
                    {
                        _timerRepository.SaveTimerState(CloneTimer(sourceTimer, teamName));
                    }
                }
            }
        }
    }

    private static TeamProgress CloneProgress(TeamProgress source, string targetScope)
    {
        return new TeamProgress
        {
            TeamId = targetScope,
            CurrentStep = source.CurrentStep,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static TimerState CloneTimer(TimerState source, string targetScope)
    {
        return new TimerState
        {
            TeamName = targetScope,
            ManualTimer = new ManualTimerState
            {
                Status = source.ManualTimer.Status,
                StartedAt = source.ManualTimer.StartedAt,
                AccumulatedSeconds = source.ManualTimer.AccumulatedSeconds
            },
            TimerStartedAt = source.TimerStartedAt,
            ChallengeTimes = new Dictionary<string, int>(source.ChallengeTimes)
        };
    }
}
