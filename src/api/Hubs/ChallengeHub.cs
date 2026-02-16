using Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

public class ChallengeHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var session = Context.GetHttpContext()?.Items["User"] as AuthSession;
        if (session?.Team != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, session.Team);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var session = Context.GetHttpContext()?.Items["User"] as AuthSession;
        if (session?.Team != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, session.Team);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
