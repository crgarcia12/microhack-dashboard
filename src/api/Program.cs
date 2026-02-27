using System.Diagnostics;
using Api.Data;
using Api.Data.Entities;
using Api.Data.EfCore;
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

// Determine data provider: only "Sqlite" and "SqlServer" are supported.
// Auto-detect from Aspire-injected "hackboxdb" connection string.
var hackboxConnStr = builder.Configuration.GetConnectionString("hackboxdb");
var configuredProvider = builder.Configuration.GetValue<string>("DataProvider");

var dataProvider = string.IsNullOrWhiteSpace(configuredProvider)
    ? (!string.IsNullOrWhiteSpace(hackboxConnStr) && hackboxConnStr.Contains("Server=", StringComparison.OrdinalIgnoreCase)
        ? "SqlServer"
        : "Sqlite")
    : configuredProvider.Trim();

if (!string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase)
    && !string.Equals(dataProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Unsupported DataProvider '{dataProvider}'. Supported values are SqlServer and Sqlite.");
}

if (string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase)
    && string.IsNullOrWhiteSpace(hackboxConnStr))
{
    throw new InvalidOperationException(
        "DataProvider is SqlServer but ConnectionStrings:hackboxdb is not configured.");
}

var defaultSqliteConnStr = IsRunningUnderTestHost()
    ? $"Data Source={Path.Combine(Path.GetTempPath(), $"hackbox-tests-{Guid.NewGuid():N}.db")}"
    : "Data Source=hackbox.db";

var effectiveHackboxConnStr = string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase)
    ? hackboxConnStr!
    : (string.IsNullOrWhiteSpace(hackboxConnStr) ? defaultSqliteConnStr : hackboxConnStr);

builder.Services.AddSingleton(CreateDataStoreInfo(dataProvider, effectiveHackboxConnStr));

builder.Services.AddDbContext<HackboxDbContext>(options =>
{
    if (string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(effectiveHackboxConnStr);
    }
    else
    {
        options.UseSqlite(effectiveHackboxConnStr);
    }
});

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

// Register auth service
var usersFilePath = Path.Combine(builder.Environment.ContentRootPath, "config-data", "users.json");
var microhacksFilePath = Path.Combine(builder.Environment.ContentRootPath, "config-data", "microhacks.json");

builder.Services.AddScoped<IAuthService>(sp =>
    new AuthService(sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<ISessionRepository>()));

// Register credential service
var credentialsFilePath = Path.Combine(builder.Environment.ContentRootPath, "config-data", "credentials.json");

builder.Services.AddScoped<ICredentialService>(sp =>
    new CredentialService(sp.GetRequiredService<ICredentialRepository>(), sp.GetRequiredService<ILogger<CredentialService>>()));

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
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<IProgressRepository>(),
        sp.GetRequiredService<ITimerRepository>(),
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
    else if (string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        // SQL Server database may exist without schema (pre-created by infra).
        // Create tables if they are missing.
        var creator = db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
        try
        {
            creator.CreateTables();
            shouldSeedFromFiles = true;
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2714)
        { /* Error 2714 = "object already exists" — safe to ignore */ }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: CreateTables failed: {ex.Message}");
            // Rethrow so the app doesn't start with a broken schema
            throw;
        }
    }

    if (string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        EnsureSqlServerTeamsColumns(db);
    }

    if (string.Equals(dataProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        EnsureSqliteTeamsColumns(db);
    }

    if (shouldSeedFromFiles)
    {
        // Initial bootstrap only: load seed files once when schema is created.
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        AuthService.SeedFromFileIfEmpty(userRepo, usersFilePath);

        SeedMicrohacksFromFileIfEmpty(db, microhacksFilePath);

        var credentialRepo = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();
        CredentialService.SeedFromFileIfEmpty(credentialRepo, credentialsFilePath);

        var timerRepo = scope.ServiceProvider.GetRequiredService<ITimerRepository>();
        SeedTimersFromFileIfEmpty(timerRepo, Path.Combine(builder.Environment.ContentRootPath, "config-data", "timers.json"));
    }

    EnsureTeamsAssignedToMicrohack(db);
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

// Microhack management endpoints
app.MapMicrohackManagementEndpoints();

// User management endpoints (CRUD for teams, hackers, coaches)
app.MapUserManagementEndpoints();

// SignalR hubs
app.MapHub<ChallengeHub>("/hubs/progress");

app.Run();

static bool IsRunningUnderTestHost()
{
    return AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
    {
        var name = assembly.GetName().Name;
        return string.Equals(name, "testhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "xunit.core", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "xunit.v3.core", StringComparison.OrdinalIgnoreCase);
    });
}

static DataStoreInfo CreateDataStoreInfo(string provider, string connectionString)
{
    if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        var server = ReadConnectionStringValue(connectionString, "Server", "Data Source");
        var database = ReadConnectionStringValue(connectionString, "Database", "Initial Catalog");
        var target = !string.IsNullOrWhiteSpace(server) && !string.IsNullOrWhiteSpace(database)
            ? $"{server}/{database}"
            : connectionString;

        return new DataStoreInfo
        {
            Provider = "SqlServer",
            Target = target
        };
    }

    var dataSource = ReadConnectionStringValue(connectionString, "Data Source");
    return new DataStoreInfo
    {
        Provider = "Sqlite",
        Target = string.IsNullOrWhiteSpace(dataSource) ? connectionString : dataSource
    };
}

static string ReadConnectionStringValue(string connectionString, params string[] keys)
{
    var segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var segment in segments)
    {
        var index = segment.IndexOf('=');
        if (index <= 0) continue;

        var key = segment[..index].Trim();
        var value = segment[(index + 1)..].Trim();
        if (keys.Any(candidate => key.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            return value;
        }
    }

    return string.Empty;
}

static void EnsureSqliteTeamsColumns(HackboxDbContext db)
{
    db.Database.OpenConnection();
    try
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "PRAGMA table_info('teams');";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        AddSqliteColumnIfMissing(db, existingColumns, "enabled", "ALTER TABLE teams ADD COLUMN enabled INTEGER NOT NULL DEFAULT 1;");
        AddSqliteColumnIfMissing(db, existingColumns, "schedule_start_utc", "ALTER TABLE teams ADD COLUMN schedule_start_utc TEXT NULL;");
        AddSqliteColumnIfMissing(db, existingColumns, "schedule_end_utc", "ALTER TABLE teams ADD COLUMN schedule_end_utc TEXT NULL;");
        AddSqliteColumnIfMissing(db, existingColumns, "environment_provisioning_utc", "ALTER TABLE teams ADD COLUMN environment_provisioning_utc TEXT NULL;");
        AddSqliteColumnIfMissing(db, existingColumns, "time_zone", "ALTER TABLE teams ADD COLUMN time_zone TEXT NULL;");
        AddSqliteColumnIfMissing(db, existingColumns, "content_path", "ALTER TABLE teams ADD COLUMN content_path TEXT NULL;");
        AddSqliteColumnIfMissing(db, existingColumns, "environment_reference", "ALTER TABLE teams ADD COLUMN environment_reference TEXT NULL;");
        AddSqliteColumnIfMissing(db, existingColumns, "is_microhack", "ALTER TABLE teams ADD COLUMN is_microhack INTEGER NOT NULL DEFAULT 0;");
        AddSqliteColumnIfMissing(db, existingColumns, "microhack_id", "ALTER TABLE teams ADD COLUMN microhack_id TEXT NULL;");
    }
    finally
    {
        db.Database.CloseConnection();
    }
}

static void AddSqliteColumnIfMissing(HackboxDbContext db, ISet<string> existingColumns, string columnName, string addColumnSql)
{
    if (existingColumns.Contains(columnName))
    {
        return;
    }

    db.Database.ExecuteSqlRaw(addColumnSql);
    existingColumns.Add(columnName);
}

static void EnsureSqlServerTeamsColumns(HackboxDbContext db)
{
    AddSqlServerColumnIfMissing(
        db,
        "enabled",
        "ALTER TABLE [teams] ADD [enabled] bit NOT NULL CONSTRAINT [DF_teams_enabled] DEFAULT ((1));");
    AddSqlServerColumnIfMissing(
        db,
        "schedule_start_utc",
        "ALTER TABLE [teams] ADD [schedule_start_utc] datetime2 NULL;");
    AddSqlServerColumnIfMissing(
        db,
        "schedule_end_utc",
        "ALTER TABLE [teams] ADD [schedule_end_utc] datetime2 NULL;");
    AddSqlServerColumnIfMissing(
        db,
        "environment_provisioning_utc",
        "ALTER TABLE [teams] ADD [environment_provisioning_utc] datetime2 NULL;");
    AddSqlServerColumnIfMissing(
        db,
        "time_zone",
        "ALTER TABLE [teams] ADD [time_zone] nvarchar(100) NULL;");
    AddSqlServerColumnIfMissing(
        db,
        "content_path",
        "ALTER TABLE [teams] ADD [content_path] nvarchar(300) NULL;");
    AddSqlServerColumnIfMissing(
        db,
        "environment_reference",
        "ALTER TABLE [teams] ADD [environment_reference] nvarchar(300) NULL;");
    AddSqlServerColumnIfMissing(
        db,
        "is_microhack",
        "ALTER TABLE [teams] ADD [is_microhack] bit NOT NULL CONSTRAINT [DF_teams_is_microhack] DEFAULT ((0));");
    AddSqlServerColumnIfMissing(
        db,
        "microhack_id",
        "ALTER TABLE [teams] ADD [microhack_id] nvarchar(100) NULL;");
}

static void AddSqlServerColumnIfMissing(HackboxDbContext db, string columnName, string alterSql)
{
    var escapedColumnName = columnName.Replace("'", "''", StringComparison.Ordinal);
    var sql = "IF COL_LENGTH('teams', '" + escapedColumnName + "') IS NULL BEGIN " + alterSql + " END";
    db.Database.ExecuteSqlRaw(sql);
}

static void SeedMicrohacksFromFileIfEmpty(HackboxDbContext db, string microhacksFilePath)
{
    if (db.Teams.Any(t => t.IsMicrohack)) return;
    if (!File.Exists(microhacksFilePath)) return;

    try
    {
        var json = File.ReadAllText(microhacksFilePath);
        var config = System.Text.Json.JsonSerializer.Deserialize<MicrohackSeedConfig>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (config?.Microhacks == null || config.Microhacks.Count == 0) return;

        foreach (var seed in config.Microhacks)
        {
            var microhackId = seed.MicrohackId?.Trim();
            if (string.IsNullOrWhiteSpace(microhackId)) continue;
            if (db.Teams.Any(t => t.Name == microhackId)) continue;

            var startUtc = ToUtc(seed.StartDate ?? DateTime.UtcNow);
            var provisioningUtc = ToUtc(seed.EnvironmentProvisioningDate ?? startUtc.AddDays(-1));
            var endUtc = ToUtc(seed.EndDate ?? startUtc.Date.AddDays(1));
            if (endUtc <= startUtc) endUtc = startUtc.Date.AddDays(1);
            if (provisioningUtc > startUtc) provisioningUtc = startUtc.AddDays(-1);

            db.Teams.Add(new TeamEntity
            {
                Name = microhackId,
                IsMicrohack = true,
                Enabled = seed.Enabled ?? true,
                ScheduleStartUtc = startUtc,
                ScheduleEndUtc = endUtc,
                EnvironmentProvisioningUtc = provisioningUtc,
                TimeZone = seed.TimeZone,
                ContentPath = seed.ContentPath,
                EnvironmentReference = seed.EnvironmentReference
            });
        }

        db.SaveChanges();

        foreach (var seed in config.Microhacks)
        {
            var microhackId = seed.MicrohackId?.Trim();
            if (string.IsNullOrWhiteSpace(microhackId)) continue;

            var teamNames = seed.Teams?
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            foreach (var teamName in teamNames)
            {
                var existingTeam = db.Teams.FirstOrDefault(t => !t.IsMicrohack && t.Name == teamName);
                if (existingTeam == null)
                {
                    db.Teams.Add(new TeamEntity
                    {
                        Name = teamName,
                        IsMicrohack = false,
                        MicrohackId = microhackId
                    });
                }
                else
                {
                    existingTeam.MicrohackId = microhackId;
                }
            }
        }

        db.SaveChanges();
    }
    catch (Exception)
    {
        // Silently skip malformed microhack seed file
    }
}

static void EnsureTeamsAssignedToMicrohack(HackboxDbContext db)
{
    var defaultMicrohackId = db.Teams
        .AsNoTracking()
        .Where(t => t.IsMicrohack)
        .OrderBy(t => t.Name)
        .Select(t => t.Name)
        .FirstOrDefault();

    if (string.IsNullOrWhiteSpace(defaultMicrohackId)) return;

    var unassignedTeams = db.Teams
        .Where(t => !t.IsMicrohack && t.MicrohackId == null)
        .ToList();
    if (unassignedTeams.Count == 0) return;

    foreach (var team in unassignedTeams)
    {
        team.MicrohackId = defaultMicrohackId;
    }

    db.SaveChanges();
}

static DateTime ToUtc(DateTime dateTime)
{
    return dateTime.Kind switch
    {
        DateTimeKind.Utc => dateTime,
        DateTimeKind.Local => dateTime.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
    };
}

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

public sealed class MicrohackSeedConfig
{
    public List<MicrohackSeedItem> Microhacks { get; set; } = new();
}

public sealed class MicrohackSeedItem
{
    public string MicrohackId { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? EnvironmentProvisioningDate { get; set; }
    public bool? Enabled { get; set; }
    public string? TimeZone { get; set; }
    public string? ContentPath { get; set; }
    public string? EnvironmentReference { get; set; }
    public List<string>? Teams { get; set; }
}
