using Api.Models;

namespace Api.Data;

public interface IHackStateRepository
{
    HackState GetState();
    void UpdateState(HackState state);
}
