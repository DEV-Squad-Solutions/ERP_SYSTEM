using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Entities.ReferenceData;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Persistence.Realtime;

namespace MiniErp.Tests.Realtime;

public sealed class RealtimeChangeInterceptorTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Collector_TracksOutboxAlongsideBusinessChange()
    {
        await using var database = await RealtimeTestDatabase.CreateAsync();
        database.Context.Countries.Add(CreateCountry());

        database.Interceptor.EnqueueNotifications(database.Context);

        var message = Assert.Single(
            database.Context.ChangeTracker.Entries<RealtimeOutboxMessage>());
        Assert.Equal(EntityState.Added, message.State);
        Assert.Equal(7, message.Entity.CompanyId);
    }

    [Fact]
    public async Task AddUpdateDelete_CreateCompanyScopedOutboxEvents()
    {
        await using var database = await RealtimeTestDatabase.CreateAsync();

        database.Context.Countries.Add(CreateCountry());
        await database.Context.SaveChangesAsync();

        database.Context.ChangeTracker.Clear();
        var country = await database.Context.Countries.SingleAsync();
        country.Name = "Updated country";
        await database.Context.SaveChangesAsync();

        database.Context.ChangeTracker.Clear();
        country = await database.Context.Countries.SingleAsync();
        database.Context.Countries.Remove(country);
        await database.Context.SaveChangesAsync();

        database.Context.ChangeTracker.Clear();
        var messages = await database.Context.RealtimeOutboxMessages
            .AsNoTracking()
            .ToListAsync();

        Assert.Equal(3, messages.Count);
        Assert.All(messages, message => Assert.Equal(7, message.CompanyId));

        var notifications = messages
            .Select(message => new
            {
                Message = message,
                Notification = Deserialize(message)
            })
            .ToDictionary(
                item => Assert.Single(item.Notification.Changes).Action);

        Assert.Equal(
            new[] { "Added", "Deleted", "Updated" },
            notifications.Keys.Order().ToArray());

        foreach (var item in notifications.Values)
        {
            Assert.Equal(item.Message.Id, item.Notification.EventId);
            var change = Assert.Single(item.Notification.Changes);
            Assert.Equal(nameof(Country), change.Resource);
            Assert.Equal("1", change.EntityId);
            Assert.Empty(change.StoreIds);
        }

        var deletedCountry = await database.Context.Countries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync();
        Assert.True(deletedCountry.IsDeleted);
    }

    [Fact]
    public async Task RolledBackTransaction_DoesNotPersistOutboxEvent()
    {
        await using var database = await RealtimeTestDatabase.CreateAsync();
        database.Context.Countries.Add(CreateCountry());
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        await database.Context.Database.ExecuteSqlRawAsync(
            "DELETE FROM RealtimeOutboxMessages");

        await using (var transaction =
            await database.Context.Database.BeginTransactionAsync())
        {
            var country = await database.Context.Countries.SingleAsync();
            country.Name = "Must roll back";
            await database.Context.SaveChangesAsync();

            Assert.Single(
                await database.Context.RealtimeOutboxMessages.ToListAsync());

            await transaction.RollbackAsync();
        }

        database.Context.ChangeTracker.Clear();
        Assert.Empty(
            await database.Context.RealtimeOutboxMessages.ToListAsync());
        Assert.Equal(
            "Initial country",
            (await database.Context.Countries.AsNoTracking().SingleAsync()).Name);
    }

    private static RealtimeChangeNotification Deserialize(
        RealtimeOutboxMessage message) =>
        JsonSerializer.Deserialize<RealtimeChangeNotification>(
            message.Payload,
            SerializerOptions)
        ?? throw new InvalidOperationException("Outbox payload was empty.");

    private static Country CreateCountry() => new()
    {
        Id = 1,
        Code = "EG",
        Name = "Initial country",
        ArabicName = "مصر",
        IsActive = true
    };

    private sealed class RealtimeTestDatabase : IAsyncDisposable
    {
        private RealtimeTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context,
            RealtimeChangeInterceptor interceptor)
        {
            Connection = connection;
            Context = context;
            Interceptor = interceptor;
        }

        private SqliteConnection Connection { get; }

        public ApplicationDbContext Context { get; }

        public RealtimeChangeInterceptor Interceptor { get; }

        public static async Task<RealtimeTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var httpContextAccessor = new TestHttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[]
                            {
                                new Claim(CustomClaimTypes.CompanyId, "7"),
                                new Claim("sub", Guid.NewGuid().ToString())
                            },
                            "test"))
                }
            };
            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(
                    2026,
                    8,
                    8,
                    7,
                    0,
                    0,
                    TimeSpan.Zero));
            var realtimeInterceptor = new RealtimeChangeInterceptor(
                httpContextAccessor,
                timeProvider);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(
                    new AuditableEntityInterceptor(
                        httpContextAccessor,
                        timeProvider))
                .Options;
            var context = new ApplicationDbContext(
                options,
                realtimeInterceptor);

            await CreateSchemaAsync(context);
            return new RealtimeTestDatabase(
                connection,
                context,
                realtimeInterceptor);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static Task CreateSchemaAsync(ApplicationDbContext context) =>
            context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE Countries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    ArabicName TEXT NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE RealtimeOutboxMessages (
                    Id TEXT PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    OccurredAtUtc TEXT NOT NULL,
                    Payload TEXT NOT NULL,
                    DispatchedAtUtc TEXT NULL,
                    AttemptCount INTEGER NOT NULL,
                    NextAttemptAtUtc TEXT NULL,
                    LastError TEXT NULL
                );
                """);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
