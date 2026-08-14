using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Seeding;

namespace MiniErp.Api.Startup;

public sealed class DatabaseRecoveryService(
    IServiceProvider services,
    IConfiguration configuration,
    StartupDatabaseInitializer initializer,
    StartupDatabaseStatus status,
    ILogger<DatabaseRecoveryService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (status.GetSnapshot().State != "Ready")
            {
                var applyMigrations = configuration.GetValue(
                    "Database:ApplyMigrationsOnStartup",
                    true);
                await initializer.InitializeAsync(
                    applyMigrations: true,
                    seedEnabled: configuration.GetValue("Seed:Enabled", false),
                    canConnectAsync: CanConnectAsync,
                    applyMigrationsAsync: cancellationToken =>
                        PrepareDatabaseAsync(
                            applyMigrations,
                            cancellationToken),
                    seedAsync: cancellationToken => DevelopmentDataSeeder.SeedAsync(
                        services,
                        configuration,
                        cancellationToken),
                    status: status,
                    logger: logger,
                    cancellationToken: stoppingToken);
            }

            await Task.Delay(RetryDelay, stoppingToken);

            var snapshot = status.GetSnapshot();
            if (snapshot.IsReady && snapshot.State == "Ready")
            {
                try
                {
                    if (!await CanConnectAsync(stoppingToken))
                    {
                        status.MarkDegraded("Connectivity");
                        logger.LogWarning(
                            "Database connectivity was lost; recovery will be attempted.");
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    status.MarkDegraded("Connectivity");
                    logger.LogError(
                        exception,
                        "Database connectivity probe failed; recovery will be attempted.");
                }
            }
        }
    }

    private async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.Database.CanConnectAsync(cancellationToken);
    }

    private async Task PrepareDatabaseAsync(
        bool applyMigrations,
        CancellationToken cancellationToken)
    {
        if (applyMigrations)
        {
            await services.ApplyPendingMigrationsAsync(cancellationToken);
        }

        var connectionString = configuration.GetConnectionString(
            "DefaultConnection") ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        SqlServerObjectsInstaller.Install(connection);
    }
}
