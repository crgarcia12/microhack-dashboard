using Api.Data;
using Api.Models;

namespace Api.Services;

public class HackStateService : IHackStateService
{
    private readonly IHackStateRepository _stateRepository;
    private readonly IHackConfigRepository _configRepository;
    private readonly ILogger<HackStateService> _logger;

    public HackStateService(
        IHackStateRepository stateRepository,
        IHackConfigRepository configRepository,
        ILogger<HackStateService> logger)
    {
        _stateRepository = stateRepository;
        _configRepository = configRepository;
        _logger = logger;
    }

    public HackState GetState()
    {
        return _stateRepository.GetState();
    }

    public HackConfig GetConfig()
    {
        return _configRepository.GetConfig();
    }

    public void SaveConfig(HackConfig config, string configuredBy)
    {
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

    public void LaunchHack(string launchedBy)
    {
        var state = _stateRepository.GetState();
        if (state.Status == "waiting" || state.Status == "configuration")
        {
            state.Status = "active";
            state.StartedAt = DateTime.UtcNow;
            state.ConfiguredBy = launchedBy;
            state.UpdatedAt = DateTime.UtcNow;
            _stateRepository.UpdateState(state);
            _logger.LogInformation("Hack launched by {User} at {Time}", launchedBy, state.StartedAt);
        }
        else
        {
            _logger.LogWarning("Cannot launch hack from status {Status}", state.Status);
        }
    }

    public bool IsHackActive()
    {
        var state = _stateRepository.GetState();
        return state.Status == "active";
    }
}
