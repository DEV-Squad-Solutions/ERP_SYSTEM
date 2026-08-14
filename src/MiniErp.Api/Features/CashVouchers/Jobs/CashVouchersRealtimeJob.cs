using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Logistics;

namespace MiniErp.Api.Features.CashVouchers.Jobs;

public sealed class CashVouchersRealtimeJob(IHubContext<UpdatesHub> hubContext, TimeProvider timeProvider)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendAsync<CashVoucher>(
            hubContext,
            timeProvider,
            request,
            additionalLegacyResources:
            [
                RealtimeResource.For<BusinessPartnerMovement>(),
                RealtimeResource.For<DriverTrip>()
            ]);
}
