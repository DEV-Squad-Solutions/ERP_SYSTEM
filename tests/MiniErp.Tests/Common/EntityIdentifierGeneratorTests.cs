using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services;

namespace MiniErp.Tests.Common;

public sealed class EntityIdentifierGeneratorTests
{
    [Theory]
    [InlineData("drv", 1, "DRV-0001")]
    [InlineData("ADJ", 10_000, "ADJ-10000")]
    public void Create_UsesSimplePrefixAndPaddedNumber(
        string prefix,
        int number,
        string expected)
    {
        var identifier = EntityIdentifierGenerator.Create(prefix, number);

        Assert.Equal(expected, identifier);
    }

    [Fact]
    public async Task GenerateUniqueAsync_UsesIndependentTenantScopes()
    {
        await using var database = await IdentifierTestDatabase.CreateAsync();

        var companyOneFirst = await database.GenerateAsync("DRV", 1);
        var companyOneSecond = await database.GenerateAsync("DRV", 1);
        var companyTwoFirst = await database.GenerateAsync("DRV", 2);
        var globalFirst = await database.GenerateAsync("CTR", companyId: null);

        Assert.Equal("DRV-0001", companyOneFirst);
        Assert.Equal("DRV-0002", companyOneSecond);
        Assert.Equal("DRV-0001", companyTwoFirst);
        Assert.Equal("CTR-0001", globalFirst);
    }

    [Fact]
    public async Task GenerateUniqueAsync_SkipsAnExistingPaddedIdentifier()
    {
        await using var database = await IdentifierTestDatabase.CreateAsync();
        await database.AddExistingIdentifierAsync("DRV-0001");

        var identifier = await database.GenerateAsync("DRV", companyId: 1);

        Assert.Equal("DRV-0002", identifier);
    }

    [Fact]
    public async Task GenerateUniqueAsync_AllocatesConcurrentNumbersAtomically()
    {
        await using var database = await IdentifierTestDatabase.CreateAsync();

        var identifiers = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => database.GenerateOnNewConnectionAsync(
                    prefix: "DRV",
                    companyId: 1)));

        Assert.Equal(
            Enumerable.Range(1, 8).Select(number => $"DRV-{number:D4}"),
            identifiers.OrderBy(identifier => identifier));
    }

    private sealed class IdentifierTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection anchorConnection;
        private readonly string connectionString;

        private IdentifierTestDatabase(
            SqliteConnection anchorConnection,
            string connectionString)
        {
            this.anchorConnection = anchorConnection;
            this.connectionString = connectionString;
        }

        public static async Task<IdentifierTestDatabase> CreateAsync()
        {
            var connectionString =
                $"Data Source=IdentifierSequences-{Guid.NewGuid():N};" +
                "Mode=Memory;Cache=Shared;Default Timeout=30";
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            await using var context = CreateContext(connection);
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE EntityIdentifierSequences (
                    Scope TEXT NOT NULL,
                    Prefix TEXT NOT NULL,
                    LastNumber INTEGER NOT NULL,
                    PRIMARY KEY (Scope, Prefix)
                );
                """);

            return new IdentifierTestDatabase(connection, connectionString);
        }

        public async Task<string> GenerateAsync(
            string prefix,
            int? companyId)
        {
            await using var context = CreateContext(anchorConnection);
            return await GenerateAsync(context, prefix, companyId);
        }

        public async Task<string> GenerateOnNewConnectionAsync(
            string prefix,
            int? companyId)
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await using var context = CreateContext(connection);
            return await GenerateAsync(context, prefix, companyId);
        }

        public async Task AddExistingIdentifierAsync(string identifier)
        {
            await using var context = CreateContext(anchorConnection);
            context.EntityIdentifierSequences.Add(
                new EntityIdentifierSequence
                {
                    Scope = "EXISTING",
                    Prefix = identifier,
                    LastNumber = 1
                });
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await anchorConnection.DisposeAsync();
        }

        private static ApplicationDbContext CreateContext(
            SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            return new ApplicationDbContext(options);
        }

        private static Task<string> GenerateAsync(
            ApplicationDbContext context,
            string prefix,
            int? companyId) =>
            EntityIdentifierGenerator.GenerateUniqueAsync(
                context,
                prefix,
                companyId,
                context.EntityIdentifierSequences
                    .Select(sequence => sequence.Prefix));
    }
}
