using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.CashManagement;

namespace MiniErp.Api.Features.CashboxTransfers.Jobs;

public sealed class CashboxTransfersRealtimeJob(IHubContext<UpdatesHub> hubContext, TimeProvider timeProvider)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendAsync<CashboxTransfer>(
            hubContext,
            timeProvider,
            request,
            additionalLegacyResources:
            [
                RealtimeResource.For<CashVoucher>()
            ]);
}
