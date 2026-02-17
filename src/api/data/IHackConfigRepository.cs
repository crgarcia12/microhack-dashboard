using Api.Models;

namespace Api.Data;

public interface IHackConfigRepository
{
    HackConfig GetConfig();
    void SaveConfig(HackConfig config);
}
