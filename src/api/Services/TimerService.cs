using System.Collections.Concurrent;
using System.Text.Json;
using Api.Data;
using Api.Models;

namespace Api.Services;

public class TimerService : ITimerService
{
    private readonly ConcurrentDictionary<string, object> _teamLocks = new();
    private readonly ITimerRepository _timerRepository;
    private readonly ILogger<TimerService> _logger;

    public TimerService(ITimerRepository timerRepository, ILogger<TimerService> logger)
    {
        _timerRepository = timerRepository;
        _logger = logger;
    }

    private object GetLock(string teamName) =>
        _teamLocks.GetOrAdd(teamName, _ => new object());

    private TimerState GetOrCreate(string teamName)
    {
        return _timerRepository.GetTimerState(teamName);
    }

    public TimerState GetTimerState(string teamName)
    {
        return GetOrCreate(teamName);
    }

    public IReadOnlyList<TimerState> GetAllTimerStates()
    {
        return _timerRepository.GetAllTimerStates();
    }

    public (ManualTimerState? State, string? Error) StartManualTimer(string teamName)
    {
        lock (GetLock(teamName))
        {
            var state = GetOrCreate(teamName);
            if (state.ManualTimer.Status == "running")
                return (null, "Timer is already running");

            state.ManualTimer.Status = "running";
            state.ManualTimer.StartedAt = DateTime.UtcNow;
            _timerRepository.SaveTimerState(state);
            return (state.ManualTimer, null);
        }
    }

    public (ManualTimerState? State, string? Error) StopManualTimer(string teamName)
    {
        lock (GetLock(teamName))
        {
            var state = GetOrCreate(teamName);
            if (state.ManualTimer.Status == "stopped")
                return (null, "Timer is already stopped");

            if (state.ManualTimer.StartedAt.HasValue)
            {
                var elapsed = (int)(DateTime.UtcNow - state.ManualTimer.StartedAt.Value).TotalSeconds;
                state.ManualTimer.AccumulatedSeconds += elapsed;
            }

            state.ManualTimer.Status = "stopped";
            state.ManualTimer.StartedAt = null;
            _timerRepository.SaveTimerState(state);
            return (state.ManualTimer, null);
        }
    }

    public ManualTimerState ResetManualTimer(string teamName)
    {
        lock (GetLock(teamName))
        {
            var state = GetOrCreate(teamName);
            state.ManualTimer.Status = "stopped";
            state.ManualTimer.StartedAt = null;
            state.ManualTimer.AccumulatedSeconds = 0;
            _timerRepository.SaveTimerState(state);
            return state.ManualTimer;
        }
    }

    public void RecordChallengeTime(string teamName, int challengeNum, int seconds)
    {
        lock (GetLock(teamName))
        {
            var state = GetOrCreate(teamName);
            state.ChallengeTimes[challengeNum.ToString()] = seconds;
            _timerRepository.SaveTimerState(state);
        }
    }

    public void SetTimerStartedAt(string teamName, DateTime? timestamp)
    {
        lock (GetLock(teamName))
        {
            var state = GetOrCreate(teamName);
            state.TimerStartedAt = timestamp;
            _timerRepository.SaveTimerState(state);
        }
    }

    public DateTime? GetTimerStartedAt(string teamName)
    {
        var state = GetOrCreate(teamName);
        return state.TimerStartedAt;
    }

    public void ClearChallengeTime(string teamName, int challengeNum)
    {
        lock (GetLock(teamName))
        {
            var state = GetOrCreate(teamName);
            state.ChallengeTimes.Remove(challengeNum.ToString());
            _timerRepository.SaveTimerState(state);
        }
    }

    public void ClearAllChallengeTimes(string teamName)
    {
        lock (GetLock(teamName))
        {
            var state = GetOrCreate(teamName);
            state.ChallengeTimes.Clear();
            state.TimerStartedAt = null;
            _timerRepository.SaveTimerState(state);
        }
    }
}
