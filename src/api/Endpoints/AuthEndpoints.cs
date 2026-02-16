using Api.Models;
using Api.Services;

namespace Api.Endpoints;

public static class AuthEndpoints
{
    private const string CookieName = "hackbox_session";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", HandleLogin)
           .WithName("Login")
           .WithTags("Auth");

        app.MapPost("/api/auth/logout", HandleLogout)
           .WithName("Logout")
           .WithTags("Auth");

        app.MapGet("/api/auth/me", HandleMe)
           .WithName("Me")
           .WithTags("Auth");
    }

    private static IResult HandleLogin(HttpContext context, LoginRequest? request, IAuthService authService)
    {
        if (request == null
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "Username and password are required" });
        }

        var user = authService.ValidateCredentials(request.Username, request.Password);
        if (user == null)
        {
            return Results.Json(new { error = "Invalid username or password" }, statusCode: 401);
        }

        var session = authService.CreateSession(user);

        context.Response.Cookies.Append(CookieName, session.SessionId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Secure = !context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment()
        });

        return Results.Ok(new LoginResponse
        {
            Username = user.Username,
            Role = user.Role,
            Team = user.Team
        });
    }

    private static IResult HandleLogout(HttpContext context, IAuthService authService)
    {
        if (context.Request.Cookies.TryGetValue(CookieName, out var sessionId)
            && !string.IsNullOrEmpty(sessionId))
        {
            authService.RemoveSession(sessionId);
        }

        context.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

        return Results.Ok(new { message = "Logged out" });
    }

    private static IResult HandleMe(HttpContext context)
    {
        if (context.Items["User"] is not AuthSession session)
        {
            return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
        }

        return Results.Ok(new LoginResponse
        {
            Username = session.Username,
            Role = session.Role,
            Team = session.Team
        });
    }
}
