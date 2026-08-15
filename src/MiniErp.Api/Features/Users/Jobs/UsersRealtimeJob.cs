using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Realtime;
using MiniErp.Infrastructure.Identity;

namespace MiniErp.Api.Features.Users.Jobs;

public sealed class UsersRealtimeJob(IHubContext<UpdatesHub> hubContext, TimeProvider timeProvider)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendAsync<ApplicationUser>(
            hubContext,
            timeProvider,
            request,
            targetGroup: RealtimeHubGroups.CompanyRole(
                request.CompanyId,
                ApplicationRoles.Admin),
            additionalLegacyResources:
            [
                RealtimeResource.For<UserCompany>()
            ]);
}
