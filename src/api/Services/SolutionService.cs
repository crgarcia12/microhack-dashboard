using System.Text.RegularExpressions;
using Api.Models;

namespace Api.Services;

public class SolutionService : ISolutionService
{
    private readonly List<Solution> _solutions = new();
    private readonly ILogger<SolutionService> _logger;
    private static readonly Regex SolutionFilePattern = new(@"^solution-(\d{3})\.md$", RegexOptions.Compiled);
    private static readonly Regex TitlePattern = new(@"^#\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    public SolutionService(string solutionsDir, ILogger<SolutionService> logger)
    {
        _logger = logger;
        LoadSolutions(solutionsDir);
    }

    private void LoadSolutions(string solutionsDir)
    {
        if (!Directory.Exists(solutionsDir))
        {
            _logger.LogWarning("Solutions directory not found: {Dir}", solutionsDir);
            return;
        }

        var files = Directory.GetFiles(solutionsDir, "solution-*.md")
            .Select(f => new { Path = f, FileName = Path.GetFileName(f) })
            .Select(f => new { f.Path, f.FileName, Match = SolutionFilePattern.Match(f.FileName) })
            .Where(f => f.Match.Success)
            .OrderBy(f => f.FileName)
            .ToList();

        foreach (var file in files)
        {
            var number = int.Parse(file.Match.Groups[1].Value);
            var content = File.ReadAllText(file.Path);
            var title = ExtractTitle(content, file.FileName);

            _solutions.Add(new Solution
            {
                Number = number,
                Title = title,
                FileName = file.FileName,
                RawMarkdown = content
            });
        }

        _logger.LogInformation("Loaded {Count} solutions", _solutions.Count);
    }

    private static string ExtractTitle(string markdown, string fileName)
    {
        var match = TitlePattern.Match(markdown);
        return match.Success ? match.Groups[1].Value.Trim() : fileName;
    }

    public IReadOnlyList<Solution> GetSolutions() => _solutions.AsReadOnly();

    public Solution? GetSolution(int number)
    {
        return _solutions.FirstOrDefault(s => s.Number == number);
    }
}
