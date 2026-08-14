using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.Catalog;

namespace MiniErp.Api.Features.ItemUnits.Jobs;

public sealed class ItemUnitsRealtimeJob(IHubContext<UpdatesHub> hubContext, TimeProvider timeProvider)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendAsync<ItemUnit>(hubContext, timeProvider, request);
}
