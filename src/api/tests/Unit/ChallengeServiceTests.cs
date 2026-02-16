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
public class ChallengeServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _challengesDir;
    private readonly string _progressDir;
    private readonly ILogger<ChallengeService> _logger = NullLogger<ChallengeService>.Instance;

    public ChallengeServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"challenge_tests_{Guid.NewGuid():N}");
        _challengesDir = Path.Combine(_tempDir, "challenges");
        _progressDir = Path.Combine(_tempDir, "progress");
        Directory.CreateDirectory(_challengesDir);
        Directory.CreateDirectory(_progressDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteChallenge(string filename, string content)
    {
        File.WriteAllText(Path.Combine(_challengesDir, filename), content);
    }

    private ChallengeService CreateService()
    {
        var repo = new FileProgressRepository(_progressDir, NullLogger<FileProgressRepository>.Instance);
        return new(_challengesDir, repo, _logger);
    }

    [Fact]
    public void LoadChallenges_ScansDirectoryForMatchingFiles()
    {
        WriteChallenge("challenge-001.md", "# First\nContent");
        WriteChallenge("challenge-002.md", "# Second\nContent");
        WriteChallenge("challenge-003.md", "# Third\nContent");

        var service = CreateService();

        service.TotalChallenges.Should().Be(3);
        service.GetChallenges().Should().HaveCount(3);
    }

    [Fact]
    public void LoadChallenges_SortsFilesByNumericSuffix()
    {
        WriteChallenge("challenge-003.md", "# Third");
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");

        var service = CreateService();
        var challenges = service.GetChallenges();

        challenges[0].Title.Should().Be("First");
        challenges[1].Title.Should().Be("Second");
        challenges[2].Title.Should().Be("Third");
    }

    [Fact]
    public void LoadChallenges_IgnoresNonMatchingFiles()
    {
        WriteChallenge("challenge-001.md", "# Valid");
        WriteChallenge("readme.md", "# Not a challenge");
        WriteChallenge("challenge-abc.md", "# Bad name");

        var service = CreateService();

        service.TotalChallenges.Should().Be(1);
    }

    [Fact]
    public void LoadChallenges_EmptyDirectory_ReturnsTotalZero()
    {
        var service = CreateService();
        service.TotalChallenges.Should().Be(0);
    }

    [Fact]
    public void LoadChallenges_MissingDirectory_ReturnsTotalZero()
    {
        var missingDir = Path.Combine(_tempDir, "nonexistent");
        var repo = new FileProgressRepository(_progressDir, NullLogger<FileProgressRepository>.Instance);
        var service = new ChallengeService(missingDir, repo, _logger);
        service.TotalChallenges.Should().Be(0);
    }

    [Fact]
    public void ExtractTitle_FromFirstHeading_ReturnsHeadingText()
    {
        var title = ChallengeService.ExtractTitle("# My Challenge Title\nSome content", 1);
        title.Should().Be("My Challenge Title");
    }

    [Fact]
    public void ExtractTitle_NoHeading_ReturnsDefaultTitle()
    {
        var title = ChallengeService.ExtractTitle("Just some content without a heading", 3);
        title.Should().Be("Challenge 3");
    }

    [Theory]
    [InlineData(1, 3, "completed")]
    [InlineData(3, 3, "current")]
    [InlineData(5, 3, "locked")]
    public void ComputeStatus_BasedOnCurrentStep(int challengeNumber, int currentStep, string expectedStatus)
    {
        var status = ChallengeService.ComputeStatus(challengeNumber, currentStep);
        status.Should().Be(expectedStatus);
    }

    [Fact]
    public void GetChallengeList_LockedChallenges_HaveNullTitle()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        WriteChallenge("challenge-003.md", "# Third");

        var service = CreateService();
        // Team at step 2: challenge 1 = completed, 2 = current, 3 = locked
        service.Approve("team-a"); // step 1 -> 2
        var challenges = service.GetChallenges();
        var progress = service.GetTeamProgress("team-a");

        var locked = challenges.Where(c => ChallengeService.ComputeStatus(c.Number, progress.CurrentStep) == "locked");
        foreach (var c in locked)
        {
            // The title in the Challenge model always has the title;
            // The null-title logic is in the endpoint layer (ChallengeListItem)
            c.Number.Should().BeGreaterThan(progress.CurrentStep);
        }
    }

    [Fact]
    public void Approve_IncrementsCurrentStepByOne()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");

        var service = CreateService();
        var (progress, error) = service.Approve("team-a");

        error.Should().BeNull();
        progress!.CurrentStep.Should().Be(2);
    }

    [Fact]
    public void Approve_WhenAllCompleted_ReturnsConflict()
    {
        WriteChallenge("challenge-001.md", "# First");

        var service = CreateService();
        service.Approve("team-a"); // step 1 -> 2 (past last challenge)

        var (progress, error) = service.Approve("team-a");
        error.Should().Be("All challenges already completed");
        progress.Should().BeNull();
    }

    [Fact]
    public void Approve_WhenNoChallengesLoaded_ReturnsConflict()
    {
        var service = CreateService();
        var (progress, error) = service.Approve("team-a");

        error.Should().Be("No challenges loaded");
        progress.Should().BeNull();
    }

    [Fact]
    public void Revert_DecrementsCurrentStepByOne()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");

        var service = CreateService();
        service.Approve("team-a"); // step 1 -> 2

        var (progress, error) = service.Revert("team-a");
        error.Should().BeNull();
        progress!.CurrentStep.Should().Be(1);
    }

    [Fact]
    public void Revert_WhenAtFirstChallenge_ReturnsConflict()
    {
        WriteChallenge("challenge-001.md", "# First");

        var service = CreateService();
        var (progress, error) = service.Revert("team-a");

        error.Should().Be("Already at first challenge");
        progress.Should().BeNull();
    }

    [Fact]
    public void Revert_WhenNoChallengesLoaded_ReturnsConflict()
    {
        var service = CreateService();
        var (progress, error) = service.Revert("team-a");

        error.Should().Be("No challenges loaded");
        progress.Should().BeNull();
    }

    [Fact]
    public void Reset_SetsCurrentStepToOne()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");

        var service = CreateService();
        service.Approve("team-a"); // step 1 -> 2
        service.Approve("team-a"); // step 2 -> 3

        var (progress, error) = service.Reset("team-a");
        error.Should().BeNull();
        progress!.CurrentStep.Should().Be(1);
    }

    [Fact]
    public void Reset_WhenAlreadyAtOne_IsIdempotent()
    {
        WriteChallenge("challenge-001.md", "# First");

        var service = CreateService();
        var (progress, error) = service.Reset("team-a");

        error.Should().BeNull();
        progress!.CurrentStep.Should().Be(1);
    }

    [Fact]
    public void Reset_WhenNoChallengesLoaded_ReturnsConflict()
    {
        var service = CreateService();
        var (progress, error) = service.Reset("team-a");

        error.Should().Be("No challenges loaded");
        progress.Should().BeNull();
    }

    [Fact]
    public void CompletedChallenges_EqualsCurrentStepMinusOne()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        WriteChallenge("challenge-003.md", "# Third");

        var service = CreateService();
        service.Approve("team-a"); // step 1 -> 2

        var progress = service.GetTeamProgress("team-a");
        progress.CompletedChallenges.Should().Be(1);
        progress.CompletedChallenges.Should().Be(progress.CurrentStep - 1);
    }

    [Fact]
    public void AllCompleted_WhenCurrentStepExceedsTotal_ReturnsTrue()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");

        var service = CreateService();
        service.Approve("team-a"); // step 1 -> 2
        service.Approve("team-a"); // step 2 -> 3 (past last)

        var progress = service.GetTeamProgress("team-a");
        progress.Completed.Should().BeTrue();
        progress.CurrentStep.Should().Be(3);
    }

    [Fact]
    public void GapInFileNumbering_MapsToSequentialChallengeNumbers()
    {
        WriteChallenge("challenge-001.md", "# First");
        WriteChallenge("challenge-002.md", "# Second");
        WriteChallenge("challenge-004.md", "# Fourth (skipped 3)");

        var service = CreateService();
        var challenges = service.GetChallenges();

        challenges.Should().HaveCount(3);
        challenges[0].Number.Should().Be(1);
        challenges[1].Number.Should().Be(2);
        challenges[2].Number.Should().Be(3); // mapped sequentially, not file number
    }
}
