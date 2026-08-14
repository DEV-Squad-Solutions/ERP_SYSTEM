using Microsoft.Extensions.Logging;

namespace MiniErp.Api.Startup;

public sealed class StartupDatabaseInitializer
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task InitializeAsync(
        bool applyMigrations,
        bool seedEnabled,
        Func<CancellationToken, Task<bool>> canConnectAsync,
        Func<CancellationToken, Task> applyMigrationsAsync,
        Func<CancellationToken, Task> seedAsync,
        StartupDatabaseStatus status,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!applyMigrations &&
                !await TryConnectAsync(canConnectAsync, status, logger, cancellationToken))
            {
                return;
            }

            if (applyMigrations &&
                !await TryRunMigrationsAsync(
                    applyMigrationsAsync,
                    status,
                    logger,
                    cancellationToken))
            {
                return;
            }

            if (seedEnabled &&
                !await TryRunSeedingAsync(seedAsync, status, logger, cancellationToken))
            {
                return;
            }

            status.MarkReady();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<bool> TryConnectAsync(
        Func<CancellationToken, Task<bool>> canConnectAsync,
        StartupDatabaseStatus status,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await canConnectAsync(cancellationToken))
            {
                return true;
            }

            throw new InvalidOperationException("The database is not reachable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            status.MarkDegraded("Connectivity");
            logger.LogError(exception, "Database connectivity check failed; recovery will be retried.");
            return false;
        }
    }

    private static async Task<bool> TryRunMigrationsAsync(
        Func<CancellationToken, Task> operation,
        StartupDatabaseStatus status,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            status.MarkDegraded("Migrations");
            logger.LogError(exception, "Database migration failed; recovery will be retried.");
            return false;
        }
    }

    private static async Task<bool> TryRunSeedingAsync(
        Func<CancellationToken, Task> operation,
        StartupDatabaseStatus status,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            status.MarkReadyWithWarnings("Seeding");
            logger.LogError(
                exception,
                "Database seeding failed. The schema is ready and seeding will be retried.");
            return false;
        }
    }
}
