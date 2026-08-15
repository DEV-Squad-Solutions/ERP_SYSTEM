using System.Security.Claims;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Api.Controllers;
using MiniErp.Api.Features.Items.Jobs;
using MiniErp.Api.Features.Invoices.Jobs;
using MiniErp.Api.Features.Users.Jobs;
using MiniErp.Api.Realtime;
using MiniErp.Application.Common.Realtime;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Tests.Realtime;

public sealed class RealtimeHangfireTests
{
    [Fact]
    public async Task FailedSave_DoesNotEnqueueJob()
    {
        var backgroundJobs = new RecordingBackgroundJobClient();
        var controller = CreateController(backgroundJobs);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.SaveThenEnqueueAsync(() =>
                throw new InvalidOperationException("SaveChanges failed.")));

        Assert.Empty(backgroundJobs.CreatedJobs);
    }

    [Fact]
    public async Task SuccessfulSave_EnqueuesJobAfterSaveCompletes()
    {
        var saveCompleted = false;
        var backgroundJobs = new RecordingBackgroundJobClient(
            onCreate: () => Assert.True(saveCompleted));
        var controller = CreateController(backgroundJobs);

        await controller.SaveThenEnqueueAsync(() =>
        {
            saveCompleted = true;
            return Task.CompletedTask;
        });

        var created = Assert.Single(backgroundJobs.CreatedJobs);
        var request = Assert.IsType<RealtimeJobRequest>(
            Assert.Single(created.Job.Args));
        Assert.Equal("Added", request.Action);
        Assert.Equal("17", request.EntityId);
        Assert.Equal(42, request.CompanyId);
    }

    [Fact]
    public async Task EnqueueFailure_AfterSave_DoesNotFailOperation()
    {
        var backgroundJobs = new RecordingBackgroundJobClient(
            exception: new InvalidOperationException("Hangfire unavailable."));
        var controller = CreateController(backgroundJobs);

        await controller.SaveThenEnqueueAsync(() => Task.CompletedTask);
    }

    [Fact]
    public void EveryFeatureRealtimeJob_HasRequiredRetryPolicy()
    {
        var jobTypes = typeof(ItemsRealtimeJob).Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                type.Namespace?.StartsWith(
                    "MiniErp.Api.Features.",
                    StringComparison.Ordinal) == true &&
                type.Name.EndsWith("RealtimeJob", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(23, jobTypes.Length);
        foreach (var jobType in jobTypes)
        {
            var method = jobType.GetMethod("ExecuteAsync");
            var retry = Assert.Single(method!.GetCustomAttributes(
                typeof(AutomaticRetryAttribute),
                inherit: true).Cast<AutomaticRetryAttribute>());
            Assert.Equal(5, retry.Attempts);
            Assert.Equal(AttemptsExceededAction.Fail, retry.OnAttemptsExceeded);
        }
    }

    [Fact]
    public async Task Job_TargetsOnlyCompanyGroup_WithMinimalStablePayload()
    {
        var clients = new RecordingHubClients();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 8, 9, 14, 30, 0, TimeSpan.Zero));
        var operationId = Guid.NewGuid();
        var request = new RealtimeJobRequest(
            OperationId: operationId,
            Action: "Updated",
            EntityId: "91",
            ActorUserId: Guid.NewGuid(),
            CompanyId: 42);
        var job = new ItemsRealtimeJob(
            new RecordingHubContext(clients),
            timeProvider);

        await job.ExecuteAsync(request);
        await job.ExecuteAsync(request);

        Assert.Equal(["company:42", "company:42"], clients.TargetedGroups);
        Assert.Equal(4, clients.Proxy.Messages.Count);
        var messages = clients.Proxy.Messages
            .Where(message => message.Method == "ReceiveEntityChanged")
            .ToArray();
        Assert.Equal(2, messages.Length);

        foreach (var message in messages)
        {
            var payload = Assert.IsType<RealtimeEntityChanged>(
                Assert.Single(message.Args));
            Assert.Equal(operationId, payload.EventId);
            Assert.Equal("Item", payload.Resource);
            Assert.Equal("Updated", payload.Action);
            Assert.Equal("91", payload.EntityId);
            Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, payload.OccurredAtUtc);
            Assert.Equal(
                5,
                payload.GetType().GetProperties().Length);
        }
    }

    [Fact]
    public async Task PermissionRestrictedJob_TargetsCompanyAdminGroup()
    {
        var clients = new RecordingHubClients();
        var job = new UsersRealtimeJob(
            new RecordingHubContext(clients),
            TimeProvider.System);

        await job.ExecuteAsync(new RealtimeJobRequest(
            OperationId: Guid.NewGuid(),
            Action: "Updated",
            EntityId: Guid.NewGuid().ToString(),
            ActorUserId: Guid.NewGuid(),
            CompanyId: 42));

        Assert.Equal(["company:42:role:Admin"], clients.TargetedGroups);
    }

    [Fact]
    public async Task CompositeJob_LegacyEventIncludesDerivedCacheResources()
    {
        var clients = new RecordingHubClients();
        var job = new InvoicesRealtimeJob(
            new RecordingHubContext(clients),
            TimeProvider.System);

        await job.ExecuteAsync(new RealtimeJobRequest(
            OperationId: Guid.NewGuid(),
            Action: "Updated",
            EntityId: "73",
            ActorUserId: Guid.NewGuid(),
            CompanyId: 42));

        var legacyMessage = Assert.Single(
            clients.Proxy.Messages,
            message => message.Method == "entityChanged");
        var payload = Assert.IsType<RealtimeChangeNotification>(
            Assert.Single(legacyMessage.Args));
        var resources = payload.Changes
            .Select(change => change.Resource)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Invoice", resources);
        Assert.Contains("InvoiceLine", resources);
        Assert.Contains("ItemMovement", resources);
        Assert.Contains("ItemStoreBalance", resources);
        Assert.Contains("BusinessPartnerMovement", resources);
        Assert.Equal(
            "73",
            Assert.Single(
                payload.Changes,
                change => change.Resource == "Invoice").EntityId);
        Assert.All(
            payload.Changes.Where(change => change.Resource != "Invoice"),
            change => Assert.Null(change.EntityId));
    }

    [Fact]
    public void RuntimeModel_HasNoOutboxInfrastructure()
    {
        Assert.Null(typeof(ApplicationDbContext).GetProperty(
            "RealtimeOutboxMessages"));

        var runtimeTypes = typeof(ApplicationDbContext).Assembly
            .GetTypes()
            .Where(type => type.Namespace is null ||
                !type.Namespace.EndsWith(
                    ".Persistence.Migrations",
                    StringComparison.Ordinal));
        Assert.DoesNotContain(
            runtimeTypes,
            type => type.Name.Contains("Outbox", StringComparison.Ordinal));
    }

    private static TestRealtimeController CreateController(
        IBackgroundJobClient backgroundJobs)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(backgroundJobs)
            .BuildServiceProvider();
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", userId.ToString())],
                authenticationType: "test"))
        };
        return new TestRealtimeController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
            }
        };
    }

    private sealed class TestRealtimeController : ApiControllerBase
    {
        public async Task SaveThenEnqueueAsync(Func<Task> save)
        {
            await save();
            TryEnqueueRealtime<ItemsRealtimeJob>(
                "Added",
                17,
                request => job => job.ExecuteAsync(request),
                companyId: 42);
        }
    }

    private sealed class RecordingBackgroundJobClient(
        Action? onCreate = null,
        Exception? exception = null) : IBackgroundJobClient
    {
        public List<(Job Job, IState State)> CreatedJobs { get; } = [];

        public string Create(Job job, IState state)
        {
            onCreate?.Invoke();
            if (exception is not null)
            {
                throw exception;
            }

            CreatedJobs.Add((job, state));
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(
            string jobId,
            IState state,
            string expectedState) => true;
    }

    private sealed class RecordingHubContext(RecordingHubClients clients)
        : IHubContext<UpdatesHub>
    {
        public IHubClients Clients { get; } = clients;

        public IGroupManager Groups { get; } = new RecordingGroupManager();
    }

    private sealed class RecordingHubClients : IHubClients
    {
        public RecordingClientProxy Proxy { get; } = new();

        public List<string> TargetedGroups { get; } = [];

        public IClientProxy All =>
            throw new InvalidOperationException("Clients.All is forbidden.");

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) =>
            throw new InvalidOperationException("Broad targeting is forbidden.");

        public IClientProxy Client(string connectionId) => Proxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;

        public IClientProxy Group(string groupName)
        {
            TargetedGroups.Add(groupName);
            return Proxy;
        }

        public IClientProxy GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;

        public IClientProxy User(string userId) => Proxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> Messages { get; } = [];

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            Messages.Add((method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
