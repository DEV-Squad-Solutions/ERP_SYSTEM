namespace MiniErp.Infrastructure.Persistence.Realtime;

public sealed class RealtimeOutboxMessage
{
    public Guid Id { get; set; }

    public int CompanyId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTime? DispatchedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public DateTime? NextAttemptAtUtc { get; set; }

    public string? LastError { get; set; }
}
