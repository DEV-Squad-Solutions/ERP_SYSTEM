using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Api.Features.Accounts.Jobs;

public sealed class AccountsRealtimeJob(
    IHubContext<UpdatesHub> hubContext,
    TimeProvider timeProvider)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendAsync<Account>(
            hubContext,
            timeProvider,
            request);
}
