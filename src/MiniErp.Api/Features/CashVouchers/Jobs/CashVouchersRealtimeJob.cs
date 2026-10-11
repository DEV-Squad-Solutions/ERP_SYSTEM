using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Logistics;

namespace MiniErp.Api.Features.CashVouchers.Jobs;

public sealed class CashVouchersRealtimeJob(IHubContext<UpdatesHub> hubContext, TimeProvider timeProvider, ICashboxRealtimeAudience? audience = null)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendScopedAsync<CashVoucher>(
            hubContext,
            timeProvider,
            request,
            audience,
            additionalLegacyResources:
            [
                RealtimeResource.For<BusinessPartnerMovement>(),
                RealtimeResource.For<DriverTrip>()
            ]);
}
