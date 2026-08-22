using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Api.Startup;

namespace MiniErp.Tests.Startup;

public sealed class StartupDatabaseInitializerTests
{
    [Fact]
    public async Task RecoveryService_StartAsync_WaitsForInitialAttemptToMarkReady()
    {
        var status = new StartupDatabaseStatus();
        var initializationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowInitializationToComplete = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var initializer = new StartupDatabaseInitializer();
        using var service = new TestDatabaseRecoveryService(
            status,
            cancellationToken => InitializeAsync(
                initializer,
                status,
                async token =>
                {
                    Interlocked.Increment(ref attempts);
                    initializationStarted.SetResult();
                    await allowInitializationToComplete.Task.WaitAsync(token);
                },
                cancellationToken));

        var startTask = service.StartAsync(CancellationToken.None);
        await initializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(startTask.IsCompleted);
        Assert.False(status.GetSnapshot().IsReady);

        allowInitializationToComplete.SetResult();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(status.GetSnapshot().IsReady);
        Assert.Equal("Ready", status.GetSnapshot().State);
        Assert.Equal(1, Volatile.Read(ref attempts));
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RecoveryService_StartAsync_AllowsDegradedStartupAndRetriesInBackground()
    {
        var status = new StartupDatabaseStatus();
        var recoveryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRecoveryToComplete = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var initializer = new StartupDatabaseInitializer();
        using var service = new TestDatabaseRecoveryService(
            status,
            cancellationToken => InitializeAsync(
                initializer,
                status,
                async token =>
                {
                    if (Interlocked.Increment(ref attempts) == 1)
                    {
                        throw new InvalidOperationException(
                            "Database unavailable.");
                    }

                    recoveryStarted.TrySetResult();
                    await allowRecoveryToComplete.Task.WaitAsync(token);
                },
                cancellationToken));

        await service.StartAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        await recoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(status.GetSnapshot().IsReady);
        Assert.Equal("Degraded", status.GetSnapshot().State);
        Assert.Equal("Migrations", status.GetSnapshot().FailurePhase);

        allowRecoveryToComplete.SetResult();
        await WaitUntilAsync(
            () => status.GetSnapshot().IsReady,
            TimeSpan.FromSeconds(5));

        Assert.Equal(2, Volatile.Read(ref attempts));
        Assert.Equal("Ready", status.GetSnapshot().State);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MigrationFailure_ThenSuccess_RecoversToReady()
    {
        var initializer = new StartupDatabaseInitializer();
        var status = new StartupDatabaseStatus();
        var attempts = 0;

        Task Migrate(CancellationToken _)
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new InvalidOperationException("Database unavailable.");
            }

            return Task.CompletedTask;
        }

        await InitializeAsync(initializer, status, Migrate);
        Assert.False(status.GetSnapshot().IsReady);
        Assert.Equal("Migrations", status.GetSnapshot().FailurePhase);

        await InitializeAsync(initializer, status, Migrate);
        Assert.True(status.GetSnapshot().IsReady);
        Assert.Equal("Ready", status.GetSnapshot().State);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ConcurrentCalls_DoNotRunMigrationConcurrently()
    {
        var initializer = new StartupDatabaseInitializer();
        var status = new StartupDatabaseStatus();
        var active = 0;
        var maximumActive = 0;

        async Task Migrate(CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref active);
            maximumActive = Math.Max(maximumActive, current);
            await Task.Delay(30, cancellationToken);
            Interlocked.Decrement(ref active);
        }

        await Task.WhenAll(
            InitializeAsync(initializer, status, Migrate),
            InitializeAsync(initializer, status, Migrate));

        Assert.Equal(1, maximumActive);
    }

    [Fact]
    public async Task Cancellation_IsNotConvertedToDegradedState()
    {
        var initializer = new StartupDatabaseInitializer();
        var status = new StartupDatabaseStatus();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            InitializeAsync(
                initializer,
                status,
                _ => Task.CompletedTask,
                cancellation.Token));

        Assert.Equal("Initializing", status.GetSnapshot().State);
    }

    [Fact]
    public async Task SeedingFailure_KeepsSchemaReadyAndMarksWarning()
    {
        var initializer = new StartupDatabaseInitializer();
        var status = new StartupDatabaseStatus();

        await initializer.InitializeAsync(
            applyMigrations: true,
            seedEnabled: true,
            canConnectAsync: _ => Task.FromResult(true),
            applyMigrationsAsync: _ => Task.CompletedTask,
            seedAsync: _ => throw new InvalidOperationException("Seed failed."),
            status: status,
            logger: NullLogger.Instance);

        Assert.True(status.GetSnapshot().IsReady);
        Assert.Equal("ReadyWithWarnings", status.GetSnapshot().State);
        Assert.Equal("Seeding", status.GetSnapshot().FailurePhase);
    }

    [Fact]
    public async Task DisabledMigrations_RequiresSuccessfulConnectivityProbe()
    {
        var initializer = new StartupDatabaseInitializer();
        var status = new StartupDatabaseStatus();

        await initializer.InitializeAsync(
            applyMigrations: false,
            seedEnabled: false,
            canConnectAsync: _ => Task.FromResult(false),
            applyMigrationsAsync: _ => Task.CompletedTask,
            seedAsync: _ => Task.CompletedTask,
            status: status,
            logger: NullLogger.Instance);

        Assert.False(status.GetSnapshot().IsReady);
        Assert.Equal("Connectivity", status.GetSnapshot().FailurePhase);
    }

    [Fact]
    public async Task Middleware_ReturnsStructured503_WhenDatabaseIsNotReady()
    {
        var status = new StartupDatabaseStatus();
        var nextCalled = false;
        var middleware = new DatabaseReadinessMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            status);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/invoices";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("15", context.Response.Headers.RetryAfter);
        Assert.Contains("Database.Unavailable", body);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/swagger/index.html")]
    public async Task Middleware_AllowsDiagnosticPaths(string path)
    {
        var called = false;
        var middleware = new DatabaseReadinessMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            new StartupDatabaseStatus());
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    private static Task InitializeAsync(
        StartupDatabaseInitializer initializer,
        StartupDatabaseStatus status,
        Func<CancellationToken, Task> migrate,
        CancellationToken cancellationToken = default) =>
        initializer.InitializeAsync(
            applyMigrations: true,
            seedEnabled: false,
            canConnectAsync: _ => Task.FromResult(true),
            applyMigrationsAsync: migrate,
            seedAsync: _ => Task.CompletedTask,
            status: status,
            logger: NullLogger.Instance,
            cancellationToken: cancellationToken);

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The expected condition was not reached.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class TestDatabaseRecoveryService(
        StartupDatabaseStatus status,
        Func<CancellationToken, Task> initializeAsync)
        : DatabaseRecoveryService(
            new ServiceCollection().BuildServiceProvider(),
            new ConfigurationBuilder().Build(),
            new StartupDatabaseInitializer(),
            status,
            NullLogger<DatabaseRecoveryService>.Instance)
    {
        protected override Task InitializeOnceAsync(
            CancellationToken cancellationToken) =>
            initializeAsync(cancellationToken);
    }
}
