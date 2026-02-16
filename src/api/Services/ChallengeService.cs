using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Data;
using Api.Models;
using Microsoft.Extensions.Logging;

namespace Api.Services;

public class ChallengeService : IChallengeService
{
    private readonly List<Challenge> _challenges = new();
    private readonly ConcurrentDictionary<string, object> _teamLocks = new();
    private readonly IProgressRepository _progressRepository;
    private readonly ILogger<ChallengeService> _logger;
    private ITimerService? _timerService;
    private static readonly Regex ChallengeFilePattern = new(@"^challenge-(\d{3})\.md$", RegexOptions.Compiled);
    private static readonly Regex TitlePattern = new(@"^#\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    public int TotalChallenges => _challenges.Count;

    public ChallengeService(string challengesDir, IProgressRepository progressRepository, ILogger<ChallengeService> logger)
    {
        _progressRepository = progressRepository;
        _logger = logger;
        LoadChallenges(challengesDir);
    }

    public void SetTimerService(ITimerService timerService)
    {
        _timerService = timerService;
    }

    private void LoadChallenges(string challengesDir)
    {
        if (!Directory.Exists(challengesDir))
        {
            _logger.LogWarning("Challenges directory not found: {Dir}", challengesDir);
            return;
        }

        var files = Directory.GetFiles(challengesDir, "challenge-*.md")
            .Select(f => new { Path = f, FileName = Path.GetFileName(f) })
            .Select(f => new { f.Path, f.FileName, Match = ChallengeFilePattern.Match(f.FileName) })
            .Where(f => f.Match.Success)
            .OrderBy(f => f.FileName)
            .ToList();

        int sequenceNumber = 1;
        foreach (var file in files)
        {
            var content = File.ReadAllText(file.Path);
            var title = ExtractTitle(content, sequenceNumber);

            _challenges.Add(new Challenge
            {
                Number = sequenceNumber,
                Title = title,
                RawMarkdown = content
            });
            sequenceNumber++;
        }

        _logger.LogInformation("Loaded {Count} challenges", _challenges.Count);
    }

    public static string ExtractTitle(string markdown, int number)
    {
        var match = TitlePattern.Match(markdown);
        return match.Success ? match.Groups[1].Value.Trim() : $"Challenge {number}";
    }

    public static string ComputeStatus(int challengeNumber, int currentStep)
    {
        if (challengeNumber < currentStep) return "completed";
        if (challengeNumber == currentStep) return "current";
        return "locked";
    }

    private TeamProgress GetOrCreateProgress(string teamId)
    {
        var progress = _progressRepository.GetProgress(teamId);
        if (progress != null) return progress;

        return new TeamProgress
        {
            TeamId = teamId,
            CurrentStep = _challenges.Count > 0 ? 1 : 0,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private object GetTeamLock(string teamId)
    {
        return _teamLocks.GetOrAdd(teamId, _ => new object());
    }

    public IReadOnlyList<Challenge> GetChallenges() => _challenges.AsReadOnly();

    public Challenge? GetChallenge(int number)
    {
        return _challenges.FirstOrDefault(c => c.Number == number);
    }

    public ProgressResponse GetTeamProgress(string teamId)
    {
        var progress = GetOrCreateProgress(teamId);
        return BuildProgressResponse(teamId, progress);
    }

    public (ProgressResponse? Progress, string? Error) Approve(string teamId)
    {
        if (_challenges.Count == 0)
            return (null, "No challenges loaded");

        lock (GetTeamLock(teamId))
        {
            var progress = GetOrCreateProgress(teamId);

            if (progress.CurrentStep > _challenges.Count)
                return (null, "All challenges already completed");

            var approvedChallenge = progress.CurrentStep;
            var now = DateTime.UtcNow;

            // Automatic timing: record elapsed for the approved challenge
            if (_timerService != null)
            {
                var timerStartedAt = _timerService.GetTimerStartedAt(teamId);
                if (timerStartedAt == null)
                {
                    // First challenge or timer never started — set to now, elapsed = 0
                    _timerService.SetTimerStartedAt(teamId, now);
                    timerStartedAt = now;
                }

                var elapsed = (int)(now - timerStartedAt.Value).TotalSeconds;
                _timerService.RecordChallengeTime(teamId, approvedChallenge, elapsed);

                // Start timer for next challenge, or null if last
                var isLast = approvedChallenge >= _challenges.Count;
                _timerService.SetTimerStartedAt(teamId, isLast ? null : now);
            }

            progress.CurrentStep++;
            progress.UpdatedAt = now;
            _progressRepository.SaveProgress(progress);

            return (BuildProgressResponse(teamId, progress), null);
        }
    }

    public (ProgressResponse? Progress, string? Error) Revert(string teamId)
    {
        if (_challenges.Count == 0)
            return (null, "No challenges loaded");

        lock (GetTeamLock(teamId))
        {
            var progress = GetOrCreateProgress(teamId);

            if (progress.CurrentStep <= 1)
                return (null, "Already at first challenge");

            var revertedChallenge = progress.CurrentStep - 1;

            // Automatic timing: clear time for reverted challenge, restart timer
            if (_timerService != null)
            {
                _timerService.ClearChallengeTime(teamId, revertedChallenge);
                _timerService.SetTimerStartedAt(teamId, DateTime.UtcNow);
            }

            progress.CurrentStep--;
            progress.UpdatedAt = DateTime.UtcNow;
            _progressRepository.SaveProgress(progress);

            return (BuildProgressResponse(teamId, progress), null);
        }
    }

    public (ProgressResponse? Progress, string? Error) Reset(string teamId)
    {
        if (_challenges.Count == 0)
            return (null, "No challenges loaded");

        lock (GetTeamLock(teamId))
        {
            var progress = GetOrCreateProgress(teamId);

            // Automatic timing: clear all challenge times and timer
            _timerService?.ClearAllChallengeTimes(teamId);

            progress.CurrentStep = 1;
            progress.UpdatedAt = DateTime.UtcNow;
            _progressRepository.SaveProgress(progress);

            return (BuildProgressResponse(teamId, progress), null);
        }
    }

    private ProgressResponse BuildProgressResponse(string teamId, TeamProgress progress)
    {
        var total = _challenges.Count;
        var currentStep = total == 0 ? 0 : progress.CurrentStep;
        var completed = total > 0 && currentStep > total;

        return new ProgressResponse
        {
            TeamId = teamId,
            CurrentStep = currentStep,
            TotalChallenges = total,
            CompletedChallenges = total == 0 ? 0 : Math.Max(0, currentStep - 1),
            Completed = completed
        };
    }
}
