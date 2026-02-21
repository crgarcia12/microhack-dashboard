using Api.Models;

namespace Api.Services;

public interface IHackStateService
{
    HackState GetState();
    HackConfig GetConfig();
    void SaveConfig(HackConfig config, string configuredBy);
    bool LaunchHack(string launchedBy);
    bool PauseHack(string pausedBy);
    bool IsHackActive();
}
