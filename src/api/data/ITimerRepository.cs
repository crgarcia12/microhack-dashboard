using Api.Models;

namespace Api.Data;

public interface ITimerRepository
{
    TimerState GetTimerState(string teamName);
    IReadOnlyList<TimerState> GetAllTimerStates();
    void SaveTimerState(TimerState state);
}
