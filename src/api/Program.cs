using System.Diagnostics;
using Api.Data;
using Api.Data.EfCore;
using Api.Data.File;
using Api.Endpoints;
using Api.Hubs;
using Api.Middleware;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
const string backendContainerVersion = "0.0.3";

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // In production, all traffic arrives via the Next.js server-side proxy
        // (same-origin), so CORS is not strictly needed. We still allow the
        // configured origins for development and any direct-access scenarios.
        var allowedOrigins = builder.Configuration["API_ALLOW_ORIGINS"];
        if (!string.IsNullOrEmpty(allowedOrigins))
        {
            var origins = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithOrigins(origins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
            return;
        }

        // AppHost assigns dynamic localhost ports in dev. Allow localhost/127.0.0.1
        // origins so direct SignalR hub connections can negotiate successfully.
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
            return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("127.0.0.1");
        })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// Register services
builder.Services.AddSingleton<Api.Services.ISessionService, Api.Services.Mock.MockSessionService>();
builder.Services.AddSingleton<IConversationHandler, PlaceholderConversationHandler>();

// Register lab config
builder.Services.Configure<LabConfig>(builder.Configuration.GetSection(LabConfig.SectionName));

// Determine data provider: "File" (default), "Sqlite", or "SqlServer"
// Auto-detect from Aspire-injected "hackboxdb" connection string
var hackboxConnStr = builder.Configuration.GetConnectionString("hackboxdb");
var configuredProvider = builder.Configuration.GetValue<string>("DataProvider");

string dataProvider;
if (!string.IsNullOrEmpty(hackboxConnStr))
{
    // Connection string present — detect provider from format
    dataProvider = hackboxConnStr.Contains("Server=", StringComparison.OrdinalIgnoreCase)
        ? "SqlServer"
        : "Sqlite";
}
else
{
    dataProvider = configuredProvider ?? "File";
}

if (string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<HackboxDbContext>(options =>
        options.UseSqlServer(hackboxConnStr));
    // Repos that create their own scopes internally — safe as Singleton
    builder.Services.AddSingleton<IProgressRepository, EfProgressRepository>();
    builder.Services.AddSingleton<ITimerRepository, EfTimerRepository>();
    builder.Services.AddSingleton<ISessionRepository, EfSessionRepository>();
    // Repos that use DbContext directly — must be Scoped
    builder.Services.AddScoped<IUserRepository, EfUserRepository>();
    builder.Services.AddScoped<ICredentialRepository, EfCredentialRepository>();
    // Hack state repositories
    builder.Services.AddSingleton<IHackStateRepository, EfHackStateRepository>();
    builder.Services.AddSingleton<IHackConfigRepository, EfHackConfigRepository>();
}
else if (string.Equals(dataProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<HackboxDbContext>(options =>
        options.UseSqlite(hackboxConnStr ?? "Data Source=hackbox.db"));
    builder.Services.AddSingleton<IProgressRepository, EfProgressRepository>();
    builder.Services.AddSingleton<ITimerRepository, EfTimerRepository>();
    builder.Services.AddSingleton<ISessionRepository, EfSessionRepository>();
    builder.Services.AddScoped<IUserRepository, EfUserRepository>();
    builder.Services.AddScoped<ICredentialRepository, EfCredentialRepository>();
    // Hack state repositories
    builder.Services.AddSingleton<IHackStateRepository, EfHackStateRepository>();
    builder.Services.AddSingleton<IHackConfigRepository, EfHackConfigRepository>();
}
else
{
    // File-based repositories (default — current behavior)
    var writableRoot = builder.Environment.ContentRootPath;
    var writableProbeDir = Path.Combine(writableRoot, "config-data");
    try
    {
        Directory.CreateDirectory(writableProbeDir);
        var writableProbeFile = Path.Combine(writableProbeDir, ".write-probe");
        System.IO.File.WriteAllText(writableProbeFile, "ok");
        System.IO.File.Delete(writableProbeFile);
    }
    catch
    {
        writableRoot = Path.GetTempPath();
    }

    var progressDir = Path.Combine(writableRoot, "config-data", "progress");
    builder.Services.AddSingleton<IProgressRepository>(sp =>
        new FileProgressRepository(progressDir, sp.GetRequiredService<ILogger<FileProgressRepository>>()));

    var timerDataDir = Path.Combine(writableRoot, "config-data");
    builder.Services.AddSingleton<ITimerRepository>(sp =>
        new FileTimerRepository(timerDataDir, sp.GetRequiredService<ILogger<FileTimerRepository>>()));

    builder.Services.AddSingleton<ISessionRepository, FileSessionRepository>();

    // Hack state repositories (file-based)
    var stateDir = Path.Combine(writableRoot, "config-data");
    builder.Services.AddSingleton<IHackStateRepository>(sp =>
        new FileHackStateRepository(stateDir, sp.GetRequiredService<ILogger<FileHackStateRepository>>()));
    builder.Services.AddSingleton<IHackConfigRepository>(sp =>
        new FileHackConfigRepository(stateDir, sp.GetRequiredService<ILogger<FileHackConfigRepository>>()));
}

// Register auth service
var usersFilePath = Path.Combine(builder.Environment.ContentRootPath, "config-data", "users.json");

if (string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase)
    || string.Equals(dataProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    // DB-backed: AuthService gets IUserRepository via DI
    builder.Services.AddScoped<IAuthService>(sp =>
        new AuthService(sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<ISessionRepository>()));
}
else
{
    // File-based: AuthService loads users from JSON (legacy)
    var json = System.IO.File.ReadAllText(usersFilePath);
    var config = System.Text.Json.JsonSerializer.Deserialize<UserConfig>(json);
    var inMemoryRepo = new InMemoryUserRepository(config!.Users);
    builder.Services.AddSingleton<IUserRepository>(inMemoryRepo);
    builder.Services.AddSingleton<IAuthService>(sp =>
        new AuthService(sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<ISessionRepository>()));
}

// Register credential service
var credentialsFilePath = Path.Combine(builder.Environment.ContentRootPath, "config-data", "credentials.json");

if (string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase)
    || string.Equals(dataProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    // DB-backed: CredentialService gets ICredentialRepository via DI
    builder.Services.AddScoped<ICredentialService>(sp =>
        new CredentialService(sp.GetRequiredService<ICredentialRepository>(), sp.GetRequiredService<ILogger<CredentialService>>()));
}
else
{
    // File-based: CredentialService loads from JSON (legacy)
    builder.Services.AddSingleton<ICredentialService>(sp =>
        new CredentialService(credentialsFilePath, sp.GetRequiredService<ILogger<CredentialService>>()));
}

// Register challenge service
// In published/container mode, hackcontent is at ContentRootPath/hackcontent (via csproj Content items).
// In local dev, it's at ../../hackcontent relative to the project.
var challengesDir = Path.Combine(builder.Environment.ContentRootPath, "hackcontent", "challenges");
if (!Directory.Exists(challengesDir))
{
    challengesDir = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "hackcontent", "challenges");
}
builder.Services.AddSingleton<IChallengeService>(sp =>
    new ChallengeService(challengesDir, sp.GetRequiredService<IProgressRepository>(), sp.GetRequiredService<ILogger<ChallengeService>>()));

// Register timer service
builder.Services.AddSingleton<ITimerService>(sp =>
    new TimerService(sp.GetRequiredService<ITimerRepository>(), sp.GetRequiredService<ILogger<TimerService>>()));

// Register solution service
// In published/container mode, hackcontent is at ContentRootPath/hackcontent (via csproj Content items).
// In local dev, it's at ../../hackcontent relative to the project.
var solutionsDir = Path.Combine(builder.Environment.ContentRootPath, "hackcontent", "solutions");
if (!Directory.Exists(solutionsDir))
{
    solutionsDir = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "hackcontent", "solutions");
}
builder.Services.AddSingleton<ISolutionService>(sp =>
    new SolutionService(solutionsDir, sp.GetRequiredService<ILogger<SolutionService>>()));

// Register hack state service
builder.Services.AddSingleton<IHackStateService>(sp =>
    new HackStateService(
        sp.GetRequiredService<IHackStateRepository>(),
        sp.GetRequiredService<IHackConfigRepository>(),
        sp.GetRequiredService<ILogger<HackStateService>>()));

var app = builder.Build();
app.MapDefaultEndpoints();
app.Logger.LogInformation("Backend container version {Version}", backendContainerVersion);

// Ensure database schema exists when using a database provider
if (string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase)
    || string.Equals(dataProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
    var shouldSeedFromFiles = false;

    // EnsureCreated() is a no-op when the database already exists (e.g. created by
    // Azure Bicep provisioning). In that case we fall through to CreateTables() which
    // generates the schema inside the pre-existing empty database.
    if (db.Database.EnsureCreated())
    {
        shouldSeedFromFiles = true;
    }
    else
    {
        // Database already existed — create tables if they are missing.
        var creator = db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
        try
        {
            creator.CreateTables();
            shouldSeedFromFiles = true;
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2714)
        { /* Error 2714 = "object already exists" — safe to ignore */ }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
            when (ex.SqliteErrorCode == 1
                && ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        { /* SQLite Error 1 + "already exists" — safe to ignore */ }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: CreateTables failed: {ex.Message}");
            // Rethrow so the app doesn't start with a broken schema
            throw;
        }
    }

    if (shouldSeedFromFiles)
    {
        // Initial bootstrap only: load seed files once when schema is created.
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        AuthService.SeedFromFileIfEmpty(userRepo, usersFilePath);

        var credentialRepo = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();
        CredentialService.SeedFromFileIfEmpty(credentialRepo, credentialsFilePath);

        var timerRepo = scope.ServiceProvider.GetRequiredService<ITimerRepository>();
        SeedTimersFromFileIfEmpty(timerRepo, Path.Combine(builder.Environment.ContentRootPath, "config-data", "timers.json"));
    }
}

// Wire timer service into challenge service
if (app.Services.GetService<IChallengeService>() is ChallengeService challengeService
    && app.Services.GetService<ITimerService>() is ITimerService timerService)
{
    challengeService.SetTimerService(timerService);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        var stopwatch = Stopwatch.StartNew();
        await next();
        stopwatch.Stop();
        app.Logger.LogInformation(
            "API {Method} {Path}{QueryString} -> {StatusCode} in {ElapsedMs}ms",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds
        );
        return;
    }

    await next();
});

// Auth middleware
app.UseMiddleware<AuthMiddleware>();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health");

// Auth endpoints
app.MapAuthEndpoints();

// Chat and session endpoints
app.MapChatEndpoints();

// Credential endpoints
app.MapCredentialEndpoints();

// Lab environment endpoints
app.MapLabEndpoints();

// Challenge endpoints
app.MapChallengeEndpoints();

// Timer endpoints
app.MapTimerEndpoints();

// Solution endpoints
app.MapSolutionEndpoints();

// Dashboard endpoints
app.MapDashboardEndpoints();

// Hack state endpoints
app.MapHackStateEndpoints();

// User management endpoints (CRUD for teams, hackers, coaches)
app.MapUserManagementEndpoints();

// SignalR hubs
app.MapHub<ChallengeHub>("/hubs/progress");

app.Run();

// Seed timer states from JSON file if the database has no timer data yet.
static void SeedTimersFromFileIfEmpty(Api.Data.ITimerRepository timerRepo, string timersFilePath)
{
    var existing = timerRepo.GetAllTimerStates();
    if (existing.Count > 0) return;
    if (!File.Exists(timersFilePath)) return;

    try
    {
        var json = File.ReadAllText(timersFilePath);
        var states = System.Text.Json.JsonSerializer.Deserialize<List<Api.Models.TimerState>>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (states == null || states.Count == 0) return;

        foreach (var state in states)
        {
            if (!string.IsNullOrEmpty(state.TeamName))
            {
                timerRepo.SaveTimerState(state);
            }
        }
    }
    catch (Exception)
    {
        // Silently skip seeding if file is malformed
    }
}

// Make Program accessible for integration tests
public partial class Program { }
