using Hangfire;
using Microsoft.AspNetCore.SignalR;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Logistics;

namespace MiniErp.Api.Features.Invoices.Jobs;

public sealed class InvoicesRealtimeJob(IHubContext<UpdatesHub> hubContext, TimeProvider timeProvider)
{
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task ExecuteAsync(RealtimeJobRequest request) =>
        RealtimeEntityChangedSender.SendAsync<Invoice>(
            hubContext,
            timeProvider,
            request,
            additionalLegacyResources:
            [
                RealtimeResource.For<InvoiceLine>(),
                RealtimeResource.For<InvoiceContainerLine>(),
                RealtimeResource.For<InvoicePayment>(),
                RealtimeResource.For<ItemMovement>(),
                RealtimeResource.For<ItemStoreBalance>(),
                RealtimeResource.For<InventoryCostAllocation>(),
                RealtimeResource.For<ContainerMovement>(),
                RealtimeResource.For<BusinessPartnerMovement>(),
                RealtimeResource.For<DriverTrip>(),
                RealtimeResource.For<CashVoucher>()
            ]);
}
