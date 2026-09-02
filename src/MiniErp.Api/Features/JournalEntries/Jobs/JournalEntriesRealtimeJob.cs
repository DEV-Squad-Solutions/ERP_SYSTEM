using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Api.Features.JournalEntries.Jobs;

public sealed class JournalEntriesRealtimeJob(
    IHubContext<UpdatesHub> hubContext,
    TimeProvider timeProvider)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendAsync<JournalEntry>(
            hubContext,
            timeProvider,
            request);
}
