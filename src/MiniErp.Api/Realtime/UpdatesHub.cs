using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Application.Common.Authentication;

namespace MiniErp.Api.Realtime;

[Authorize]
public sealed class UpdatesHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!CompanyClaimResolver.TryGetCompanyId(
                Context.User,
                out var companyId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeHubGroups.Company(companyId));
        await base.OnConnectedAsync();
    }
}
