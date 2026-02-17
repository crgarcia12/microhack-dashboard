using Api.Models;

namespace Api.Services;

public interface IHackStateService
{
    HackState GetState();
    HackConfig GetConfig();
    void SaveConfig(HackConfig config, string configuredBy);
    void LaunchHack(string launchedBy);
    bool IsHackActive();
}
