using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Api.Startup;

namespace MiniErp.Tests.Startup;

public sealed class StartupDatabaseInitializerTests
{
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
}
