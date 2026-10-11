using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Api.Features.Cashboxes.Jobs;

public sealed class CashboxesRealtimeJob(IHubContext<UpdatesHub> hubContext, TimeProvider timeProvider, ICashboxRealtimeAudience? audience = null)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendScopedAsync<Cashbox>(hubContext, timeProvider, request, audience);
}
