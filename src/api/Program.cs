using Api.Data;
using Api.Data.EfCore;
using Api.Data.File;
using Api.Endpoints;
using Api.Hubs;
using Api.Middleware;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

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
        var origins = !string.IsNullOrEmpty(allowedOrigins)
            ? allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries)
            : new[] { "http://localhost:3000", "https://localhost:3000" };

        policy.WithOrigins(origins)
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
    builder.Services.AddScoped<IProgressRepository, EfProgressRepository>();
    builder.Services.AddScoped<ITimerRepository, EfTimerRepository>();
    builder.Services.AddScoped<ISessionRepository, EfSessionRepository>();
    builder.Services.AddScoped<IUserRepository, EfUserRepository>();
}
else if (string.Equals(dataProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<HackboxDbContext>(options =>
        options.UseSqlite(hackboxConnStr ?? "Data Source=hackbox.db"));
    builder.Services.AddScoped<IProgressRepository, EfProgressRepository>();
    builder.Services.AddScoped<ITimerRepository, EfTimerRepository>();
    builder.Services.AddScoped<ISessionRepository, EfSessionRepository>();
    builder.Services.AddScoped<IUserRepository, EfUserRepository>();
}
else
{
    // File-based repositories (default — current behavior)
    var progressDir = Path.Combine(builder.Environment.ContentRootPath, "config-data", "progress");
    builder.Services.AddSingleton<IProgressRepository>(sp =>
        new FileProgressRepository(progressDir, sp.GetRequiredService<ILogger<FileProgressRepository>>()));

    var timerDataDir = Path.Combine(builder.Environment.ContentRootPath, "config-data");
    builder.Services.AddSingleton<ITimerRepository>(sp =>
        new FileTimerRepository(timerDataDir, sp.GetRequiredService<ILogger<FileTimerRepository>>()));

    builder.Services.AddSingleton<ISessionRepository, FileSessionRepository>();
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
builder.Services.AddSingleton<ICredentialService>(sp =>
    new CredentialService(credentialsFilePath, sp.GetRequiredService<ILogger<CredentialService>>()));

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
var solutionsDir = Path.Combine(builder.Environment.ContentRootPath, "hackcontent", "solutions");
builder.Services.AddSingleton<ISolutionService>(sp =>
    new SolutionService(solutionsDir, sp.GetRequiredService<ILogger<SolutionService>>()));

var app = builder.Build();
app.MapDefaultEndpoints();

// Ensure database schema exists when using a database provider
if (string.Equals(dataProvider, "SqlServer", StringComparison.OrdinalIgnoreCase)
    || string.Equals(dataProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HackboxDbContext>();
    db.Database.EnsureCreated();

    // Seed users/teams from JSON if DB is empty
    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    AuthService.SeedFromFileIfEmpty(userRepo, usersFilePath);
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

// Auth middleware
app.UseMiddleware<AuthMiddleware>();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health");

// API version
app.MapGet("/api/info", () => Results.Ok(new { version = "1.0.0", framework = "spec2cloud" }))
   .WithName("ApiInfo")
   .WithTags("Info");

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

// User management endpoints (CRUD for teams, hackers, coaches)
app.MapUserManagementEndpoints();

// SignalR hubs
app.MapHub<ChallengeHub>("/hubs/progress");

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
