using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Hubs;

public class ChallengeHub : Hub
{
    public const string DashboardOperatorsGroup = "dashboard-operators";

    public override async Task OnConnectedAsync()
    {
        var session = Context.GetHttpContext()?.Items["User"] as AuthSession;
        if (session != null)
        {
            if (session.Role is "coach" or "techlead")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, DashboardOperatorsGroup);
            }

            var hackStateService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IHackStateService>();
            var config = hackStateService?.GetConfig() ?? new HackConfig();
            var scope = HackModeHelper.ResolveProgressScope(session, config);
            await Groups.AddToGroupAsync(Context.ConnectionId, scope);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var session = Context.GetHttpContext()?.Items["User"] as AuthSession;
        if (session != null)
        {
            if (session.Role is "coach" or "techlead")
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, DashboardOperatorsGroup);
            }

            var hackStateService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IHackStateService>();
            var config = hackStateService?.GetConfig() ?? new HackConfig();
            var scope = HackModeHelper.ResolveProgressScope(session, config);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, scope);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
