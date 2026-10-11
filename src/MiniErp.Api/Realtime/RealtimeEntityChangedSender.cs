using Microsoft.AspNetCore.SignalR;
using MiniErp.Application.Common.Realtime;
using MiniErp.Application.Common.Abstractions;

namespace MiniErp.Api.Realtime;

internal static class RealtimeEntityChangedSender
{
    public static async Task SendScopedAsync<TEntity>(
        IHubContext<UpdatesHub> hubContext, TimeProvider timeProvider,
        RealtimeJobRequest request, ICashboxRealtimeAudience? audience,
        params string[] additionalLegacyResources)
    {
        if (audience is null) return;
        var recipients = await audience.ResolveAsync(RealtimeResource.For<TEntity>(), request);
        foreach (var userId in recipients.Distinct())
            await SendAsync<TEntity>(hubContext, timeProvider, request,
                RealtimeHubGroups.CompanyUser(request.CompanyId, userId), additionalLegacyResources);
    }
    public static async Task SendAsync<TEntity>(
        IHubContext<UpdatesHub> hubContext,
        TimeProvider timeProvider,
        RealtimeJobRequest request,
        string? targetGroup = null,
        params string[] additionalLegacyResources)
    {
        var occurredAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var resource = RealtimeResource.For<TEntity>();
        var payload = new RealtimeEntityChanged(
            EventId: request.OperationId,
            Resource: resource,
            Action: request.Action,
            EntityId: request.EntityId,
            OccurredAtUtc: occurredAtUtc);
        var companyClients = hubContext.Clients.Group(
            targetGroup ?? RealtimeHubGroups.Company(request.CompanyId));

        await companyClients.SendAsync(
            "ReceiveEntityChanged",
            payload);

        // Keep the existing client event during the backend migration. The new
        // event above is the canonical, minimal cache-invalidation contract.
        var legacyChanges = new[] { resource }
            .Concat(additionalLegacyResources)
            .Distinct(StringComparer.Ordinal)
            .Select(legacyResource => new RealtimeEntityChange(
                Resource: legacyResource,
                Action: request.Action,
                EntityId: string.Equals(
                    legacyResource,
                    resource,
                    StringComparison.Ordinal)
                    ? request.EntityId
                    : null,
                StoreIds: []))
            .ToArray();
        var legacyPayload = new RealtimeChangeNotification(
            EventId: request.OperationId,
            OccurredAtUtc: occurredAtUtc,
            Changes: legacyChanges);
        await companyClients.SendAsync(
            "entityChanged",
            legacyPayload);
    }
}
