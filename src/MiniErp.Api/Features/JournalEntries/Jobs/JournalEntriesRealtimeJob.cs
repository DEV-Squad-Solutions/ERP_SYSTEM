using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Api.Features.JournalEntries.Jobs;

public sealed class JournalEntriesRealtimeJob(
    IHubContext<UpdatesHub> hubContext,
    TimeProvider timeProvider,
    ICashboxRealtimeAudience? audience = null)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendScopedAsync<JournalEntry>(
            hubContext,
            timeProvider,
            request, audience);
}
