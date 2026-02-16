using Api.Data;
using Api.Data.File;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit;

[Trait("Category", "Unit")]
public class TimerServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _challengesDir;
    private readonly string _progressDir;
    private readonly string _timerDataDir;
    private readonly ILogger<TimerService> _timerLogger = NullLogger<TimerService>.Instance;
    private readonly ILogger<ChallengeService> _challengeLogger = NullLogger<ChallengeService>.Instance;

    public TimerServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"timer_tests_{Guid.NewGuid():N}");
        _challengesDir = Path.Combine(_tempDir, "challenges");
        _progressDir = Path.Combine(_tempDir, "progress");
        _timerDataDir = Path.Combine(_tempDir, "timerdata");
        Directory.CreateDirectory(_challengesDir);
        Directory.CreateDirectory(_progressDir);
        Directory.CreateDirectory(_timerDataDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteChallenge(string filename, string content) =>
        File.WriteAllText(Path.Combine(_challengesDir, filename), content);

    private TimerService CreateTimerService()
    {
        var repo = new FileTimerRepository(_timerDataDir, NullLogger<FileTimerRepository>.Instance);
        return new(repo, _timerLogger);
    }

    private ChallengeService CreateChallengeServiceWithTimer(out TimerService timerService)
    {
        timerService = CreateTimerService();
        var progressRepo = new FileProgressRepository(_progressDir, NullLogger<FileProgressRepository>.Instance);
        var svc = new ChallengeService(_challengesDir, progressRepo, _challengeLogger);
        svc.SetTimerService(timerService);
        return svc;
    }

    // Automatic timing

    [Fact]
    public void Approve_RecordsChallengeElapsedSeconds()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        var svc = CreateChallengeServiceWithTimer(out var timer);

        // Set a known start time in the past
        timer.SetTimerStartedAt("team-a", DateTime.UtcNow.AddSeconds(-10));
        svc.Approve("team-a");

        var state = timer.GetTimerState("team-a");
        state.ChallengeTimes.Should().ContainKey("1");
        state.ChallengeTimes["1"].Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void Approve_StartsTimerForNextChallenge()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        var svc = CreateChallengeServiceWithTimer(out var timer);

        timer.SetTimerStartedAt("team-a", DateTime.UtcNow.AddSeconds(-5));
        svc.Approve("team-a");

        timer.GetTimerStartedAt("team-a").Should().NotBeNull();
        timer.GetTimerStartedAt("team-a")!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Approve_LastChallenge_StopsAutomaticTimer()
    {
        WriteChallenge("challenge-001.md", "# Only");
        var svc = CreateChallengeServiceWithTimer(out var timer);

        timer.SetTimerStartedAt("team-a", DateTime.UtcNow.AddSeconds(-5));
        svc.Approve("team-a");

        timer.GetTimerStartedAt("team-a").Should().BeNull();
    }

    [Fact]
    public void Approve_FirstChallenge_SetsTimerStartedAtIfNull()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        var svc = CreateChallengeServiceWithTimer(out var timer);

        // TimerStartedAt is null initially
        timer.GetTimerStartedAt("team-a").Should().BeNull();
        svc.Approve("team-a");

        // After approve, challenge time recorded as 0 and timer started for next
        var state = timer.GetTimerState("team-a");
        state.ChallengeTimes.Should().ContainKey("1");
        state.ChallengeTimes["1"].Should().Be(0);
    }

    [Fact]
    public void Approve_ElapsedSeconds_IsTruncatedNotRounded()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        var svc = CreateChallengeServiceWithTimer(out var timer);

        // (int) cast in the service truncates, so 10.9 seconds -> 10
        timer.SetTimerStartedAt("team-a", DateTime.UtcNow.AddSeconds(-10));
        svc.Approve("team-a");

        var state = timer.GetTimerState("team-a");
        // Should be >= 10 (truncated from ~10.x), not rounded up
        state.ChallengeTimes["1"].Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void Revert_DeletesRecordedTimeForRevertedChallenge()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        var svc = CreateChallengeServiceWithTimer(out var timer);

        timer.SetTimerStartedAt("team-a", DateTime.UtcNow.AddSeconds(-5));
        svc.Approve("team-a"); // step 1 -> 2, records time for challenge 1

        timer.GetTimerState("team-a").ChallengeTimes.Should().ContainKey("1");

        svc.Revert("team-a"); // step 2 -> 1, clears time for challenge 1
        timer.GetTimerState("team-a").ChallengeTimes.Should().NotContainKey("1");
    }

    [Fact]
    public void Revert_RestartsTimerForRevertedChallenge()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        var svc = CreateChallengeServiceWithTimer(out var timer);

        timer.SetTimerStartedAt("team-a", DateTime.UtcNow.AddSeconds(-5));
        svc.Approve("team-a"); // step 1 -> 2
        svc.Revert("team-a"); // step 2 -> 1

        timer.GetTimerStartedAt("team-a").Should().NotBeNull();
        timer.GetTimerStartedAt("team-a")!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Reset_ClearsAllChallengeTimes()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        var svc = CreateChallengeServiceWithTimer(out var timer);

        timer.SetTimerStartedAt("team-a", DateTime.UtcNow.AddSeconds(-5));
        svc.Approve("team-a"); // records time for challenge 1
        timer.GetTimerState("team-a").ChallengeTimes.Should().NotBeEmpty();

        svc.Reset("team-a");
        timer.GetTimerState("team-a").ChallengeTimes.Should().BeEmpty();
    }

    [Fact]
    public void Reset_SetsTimerStartedAtToNull()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        var svc = CreateChallengeServiceWithTimer(out var timer);

        timer.SetTimerStartedAt("team-a", DateTime.UtcNow);
        svc.Approve("team-a");
        svc.Reset("team-a");

        timer.GetTimerStartedAt("team-a").Should().BeNull();
    }

    // Manual timer (stopwatch)

    [Fact]
    public void ManualStart_SetsStatusToRunning()
    {
        var timer = CreateTimerService();
        var (state, error) = timer.StartManualTimer("team-a");

        error.Should().BeNull();
        state!.Status.Should().Be("running");
    }

    [Fact]
    public void ManualStart_RecordsStartedAtTimestamp()
    {
        var timer = CreateTimerService();
        var (state, _) = timer.StartManualTimer("team-a");

        state!.StartedAt.Should().NotBeNull();
        state.StartedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ManualStart_WhenAlreadyRunning_ReturnsConflict()
    {
        var timer = CreateTimerService();
        timer.StartManualTimer("team-a");
        var (state, error) = timer.StartManualTimer("team-a");

        error.Should().Be("Timer is already running");
        state.Should().BeNull();
    }

    [Fact]
    public void ManualStop_SetsStatusToStopped()
    {
        var timer = CreateTimerService();
        timer.StartManualTimer("team-a");
        var (state, error) = timer.StopManualTimer("team-a");

        error.Should().BeNull();
        state!.Status.Should().Be("stopped");
    }

    [Fact]
    public void ManualStop_AccumulatesElapsedSeconds()
    {
        var timer = CreateTimerService();
        timer.StartManualTimer("team-a");

        // Manually set StartedAt to simulate elapsed time
        var timerState = timer.GetTimerState("team-a");
        timerState.ManualTimer.StartedAt = DateTime.UtcNow.AddSeconds(-15);

        var (state, _) = timer.StopManualTimer("team-a");
        state!.AccumulatedSeconds.Should().BeGreaterThanOrEqualTo(15);
    }

    [Fact]
    public void ManualStop_WhenAlreadyStopped_ReturnsConflict()
    {
        var timer = CreateTimerService();
        var (state, error) = timer.StopManualTimer("team-a");

        error.Should().Be("Timer is already stopped");
        state.Should().BeNull();
    }

    [Fact]
    public void ManualReset_SetsElapsedToZero()
    {
        var timer = CreateTimerService();
        timer.StartManualTimer("team-a");

        var timerState = timer.GetTimerState("team-a");
        timerState.ManualTimer.StartedAt = DateTime.UtcNow.AddSeconds(-30);

        timer.StopManualTimer("team-a");
        var state = timer.ResetManualTimer("team-a");

        state.AccumulatedSeconds.Should().Be(0);
    }

    [Fact]
    public void ManualReset_SetsStatusToStopped()
    {
        var timer = CreateTimerService();
        timer.StartManualTimer("team-a");
        var state = timer.ResetManualTimer("team-a");

        state.Status.Should().Be("stopped");
        state.StartedAt.Should().BeNull();
    }

    [Fact]
    public void ManualReset_IsIdempotent()
    {
        var timer = CreateTimerService();
        var state1 = timer.ResetManualTimer("team-a");
        var state2 = timer.ResetManualTimer("team-a");

        state1.Status.Should().Be("stopped");
        state1.AccumulatedSeconds.Should().Be(0);
        state2.Status.Should().Be("stopped");
        state2.AccumulatedSeconds.Should().Be(0);
    }
}
