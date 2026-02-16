using Api.Models;

namespace Api.Services;

public interface ITimerService
{
    TimerState GetTimerState(string teamName);
    IReadOnlyList<TimerState> GetAllTimerStates();
    (ManualTimerState? State, string? Error) StartManualTimer(string teamName);
    (ManualTimerState? State, string? Error) StopManualTimer(string teamName);
    ManualTimerState ResetManualTimer(string teamName);
    void RecordChallengeTime(string teamName, int challengeNum, int seconds);
    void SetTimerStartedAt(string teamName, DateTime? timestamp);
    DateTime? GetTimerStartedAt(string teamName);
    void ClearChallengeTime(string teamName, int challengeNum);
    void ClearAllChallengeTimes(string teamName);
}
