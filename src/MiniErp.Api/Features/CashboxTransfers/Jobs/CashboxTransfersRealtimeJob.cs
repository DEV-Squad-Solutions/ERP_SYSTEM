using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Api.Features.CashboxTransfers.Jobs;

public sealed class CashboxTransfersRealtimeJob(IHubContext<UpdatesHub> hubContext, TimeProvider timeProvider, ICashboxRealtimeAudience? audience = null)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendScopedAsync<CashboxTransfer>(
            hubContext,
            timeProvider,
            request,
            audience,
            additionalLegacyResources:
            [
                RealtimeResource.For<CashVoucher>()
            ]);
}
