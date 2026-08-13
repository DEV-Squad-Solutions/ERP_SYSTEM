namespace MiniErp.Api.Startup;

public sealed class StartupDatabaseStatus
{
    private readonly object _sync = new();
    private StartupDatabaseStatusSnapshot _snapshot = new(
        State: "Initializing",
        IsReady: false,
        FailurePhase: null,
        UpdatedAtUtc: DateTimeOffset.UtcNow);

    public StartupDatabaseStatusSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return _snapshot;
        }
    }

    internal void MarkReady()
    {
        lock (_sync)
        {
            _snapshot = new StartupDatabaseStatusSnapshot(
                State: "Ready",
                IsReady: true,
                FailurePhase: null,
                UpdatedAtUtc: DateTimeOffset.UtcNow);
        }
    }

    internal void MarkDegraded(string failurePhase)
    {
        lock (_sync)
        {
            _snapshot = new StartupDatabaseStatusSnapshot(
                State: "Degraded",
                IsReady: false,
                FailurePhase: failurePhase,
                UpdatedAtUtc: DateTimeOffset.UtcNow);
        }
    }

    internal void MarkReadyWithWarnings(string failurePhase)
    {
        lock (_sync)
        {
            _snapshot = new StartupDatabaseStatusSnapshot(
                State: "ReadyWithWarnings",
                IsReady: true,
                FailurePhase: failurePhase,
                UpdatedAtUtc: DateTimeOffset.UtcNow);
        }
    }
}

public sealed record StartupDatabaseStatusSnapshot(
    string State,
    bool IsReady,
    string? FailurePhase,
    DateTimeOffset UpdatedAtUtc);
