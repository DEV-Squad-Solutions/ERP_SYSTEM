using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Api.Features.AccountStatementMappings.Jobs;

public sealed class AccountStatementMappingsRealtimeJob(
    IHubContext<UpdatesHub> hubContext,
    TimeProvider timeProvider)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendAsync<AccountStatementMapping>(
            hubContext,
            timeProvider,
            request);
}
