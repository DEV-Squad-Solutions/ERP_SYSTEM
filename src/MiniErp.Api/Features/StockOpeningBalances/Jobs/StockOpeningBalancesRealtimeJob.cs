using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Api.Features.StockOpeningBalances.Jobs;

public sealed class StockOpeningBalancesRealtimeJob(IHubContext<UpdatesHub> hubContext, TimeProvider timeProvider)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendAsync<StockOpeningBalance>(
            hubContext,
            timeProvider,
            request,
            additionalLegacyResources:
            [
                RealtimeResource.For<StockOpeningBalanceLine>(),
                RealtimeResource.For<ItemMovement>(),
                RealtimeResource.For<ItemStoreBalance>(),
                RealtimeResource.For<InventoryCostAllocation>()
            ]);
}
