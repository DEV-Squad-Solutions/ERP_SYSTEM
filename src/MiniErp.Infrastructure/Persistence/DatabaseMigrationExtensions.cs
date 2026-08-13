using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MiniErp.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplyPendingMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseMigration");

        var pendingMigrations = (await dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken))
            .ToArray();

        if (pendingMigrations.Length == 0)
        {
            logger.LogDebug("Database schema is up to date.");
            return;
        }

        logger.LogInformation(
            "Applying {MigrationCount} pending database migration(s).",
            pendingMigrations.Length);

        await dbContext.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Database migrations applied successfully.");
    }
}
