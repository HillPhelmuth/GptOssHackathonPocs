
using Microsoft.AspNetCore.SignalR;

namespace GptOssHackathonPocs.Core.Hubs;

public class ActionPlansHub : Hub
{
    // No methods required: server will push via Clients.All.SendAsync("planUpdated", plan)
}
