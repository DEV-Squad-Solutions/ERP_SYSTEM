using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Api.Startup;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Tests.Startup;

public sealed class DatabaseRecoverySeedingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupAndRecovery_DoNotSeed_EvenWhenConfigurationEnablesIt(bool seedEnabled)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ApplyMigrationsOnStartup"] = "false",
            ["Seed:Enabled"] = seedEnabled.ToString()
            // No seed password or Identity services: any real seeding attempt would fail.
        }).Build();
        var status = new StartupDatabaseStatus();
        using var recovery = new ExposedRecoveryService(provider, configuration, status);

        await recovery.InitializeAsync();
        Assert.True(status.GetSnapshot().IsReady);
        Assert.Equal("Ready", status.GetSnapshot().State);
        Assert.Null(status.GetSnapshot().FailurePhase);

        // Recovery uses the same initialization path on subsequent attempts.
        await recovery.InitializeAsync();
        Assert.True(status.GetSnapshot().IsReady);
        Assert.Equal("Ready", status.GetSnapshot().State);
        Assert.Null(status.GetSnapshot().FailurePhase);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    private sealed class ExposedRecoveryService(
        IServiceProvider services, IConfiguration configuration, StartupDatabaseStatus status)
        : DatabaseRecoveryService(services, configuration, new StartupDatabaseInitializer(), status,
            NullLogger<DatabaseRecoveryService>.Instance)
    {
        public Task InitializeAsync() => base.InitializeOnceAsync(CancellationToken.None);
    }
}
