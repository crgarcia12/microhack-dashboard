using Api.Services;

namespace Api.Middleware;

public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private const string CookieName = "hackbox_session";

    public AuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        if (context.Request.Cookies.TryGetValue(CookieName, out var sessionId)
            && !string.IsNullOrEmpty(sessionId))
        {
            var session = authService.GetSession(sessionId);
            if (session != null)
            {
                context.Items["User"] = session;
            }
        }

        await _next(context);
    }
}
