using Api.Models;
using Api.Services;

namespace Api.Endpoints;

public static class SolutionEndpoints
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".svg"
    };

    public static void MapSolutionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/solutions", HandleGetSolutions)
           .WithName("GetSolutions")
           .WithTags("Solutions");

        app.MapGet("/api/solutions/media/{filename}", HandleGetMedia)
           .WithName("GetSolutionMedia")
           .WithTags("Solutions");

        app.MapGet("/api/solutions/{number:int}", HandleGetSolution)
           .WithName("GetSolution")
           .WithTags("Solutions");
    }

    private static IResult RequireCoach(HttpContext context, out AuthSession session)
    {
        session = (context.Items["User"] as AuthSession)!;
        if (session == null)
            return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

        if (session.Role != "coach" && session.Role != "techlead")
            return Results.Json(new { error = "Forbidden" }, statusCode: 403);

        return null!;
    }

    private static IResult HandleGetSolutions(HttpContext context, ISolutionService solutionService, IChallengeService challengeService)
    {
        var denied = RequireCoach(context, out var session);
        if (denied != null) return denied;

        var solutions = solutionService.GetSolutions();
        var teamId = session.Team ?? "";
        var progress = challengeService.GetTeamProgress(teamId);

        return Results.Ok(new
        {
            solutions = solutions.Select(s => new
            {
                number = s.Number,
                title = s.Title,
                fileName = s.FileName
            }),
            totalCount = solutions.Count,
            currentStep = progress.CurrentStep
        });
    }

    private static IResult HandleGetSolution(int number, HttpContext context, ISolutionService solutionService)
    {
        var denied = RequireCoach(context, out _);
        if (denied != null) return denied;

        var solution = solutionService.GetSolution(number);
        if (solution == null)
            return Results.Json(new { error = "Solution not found", number }, statusCode: 404);

        return Results.Ok(new
        {
            number = solution.Number,
            title = solution.Title,
            fileName = solution.FileName,
            content = solution.RawMarkdown
        });
    }

    private static IResult HandleGetMedia(string filename, HttpContext context, IWebHostEnvironment env)
    {
        var denied = RequireCoach(context, out _);
        if (denied != null) return denied;

        // Validate filename: no path traversal
        if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            return Results.Json(new { error = "Invalid filename" }, statusCode: 400);

        // Validate extension
        var ext = Path.GetExtension(filename);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return Results.Json(new { error = "Unsupported file type" }, statusCode: 400);

        var mediaDir = Path.Combine(env.ContentRootPath, "hackcontent", "solutions", "media");
        var filePath = Path.GetFullPath(Path.Combine(mediaDir, filename));

        // Ensure resolved path stays within media directory
        var resolvedDir = Path.GetFullPath(mediaDir);
        if (!filePath.StartsWith(resolvedDir))
            return Results.Json(new { error = "Invalid filename" }, statusCode: 400);

        if (!File.Exists(filePath))
            return Results.Json(new { error = "Media file not found", filename }, statusCode: 404);

        return Results.File(filePath, GetContentType(ext));
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream"
    };
}
