namespace MiniErp.Application.Common.Realtime;

public sealed record RealtimeEntityChange(
    string Resource,
    string Action,
    string? EntityId,
    IReadOnlyList<int> StoreIds);

public sealed record RealtimeChangeNotification(
    Guid EventId,
    DateTime OccurredAtUtc,
    IReadOnlyList<RealtimeEntityChange> Changes);
