namespace MiniErp.Application.Common.Realtime;

public sealed record RealtimeJobRequest(
    Guid OperationId,
    string Action,
    string EntityId,
    Guid? ActorUserId,
    int CompanyId);

public sealed record RealtimeEntityChanged(
    Guid EventId,
    string Resource,
    string Action,
    string EntityId,
    DateTime OccurredAtUtc);

public static class RealtimeResource
{
    public static string For<TEntity>() => typeof(TEntity).Name;
}

public sealed record RealtimeEntityChange(
    string Resource,
    string Action,
    string? EntityId,
    IReadOnlyList<int> StoreIds);

public sealed record RealtimeChangeNotification(
    Guid EventId,
    DateTime OccurredAtUtc,
    IReadOnlyList<RealtimeEntityChange> Changes);
