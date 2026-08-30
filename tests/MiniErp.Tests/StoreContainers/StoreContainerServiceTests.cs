using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Features.StoreContainers;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Infrastructure.Services.StoreContainers;

namespace MiniErp.Tests.StoreContainers;

public sealed class StoreContainerServiceTests
{
    static StoreContainerServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task GetWorkspace_IncludesInactiveAssignedContainer()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.GetWorkspaceAsync(10);

        Assert.True(result.IsSuccess);
        Assert.Equal([100, 101, 102, 103, 105], result.Value.Containers
            .Select(container => container.Id)
            .Order()
            .ToArray());

        var inactiveAssigned = Assert.Single(
            result.Value.Containers,
            container => container.Id == 102);
        Assert.False(inactiveAssigned.IsActive);
        Assert.True(inactiveAssigned.IsAssigned);
        Assert.Equal(1_002, inactiveAssigned.StoreContainerId);

        Assert.DoesNotContain(
            result.Value.Containers,
            container => container.Id == 104);
        Assert.DoesNotContain(
            result.Value.Containers,
            container => container.CompanyId != 1);
    }

    [Fact]
    public async Task Upsert_EmptySet_SoftDeletesAllCurrentAssignments()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpsertAsync(
            new StoreContainerUpsertRequest(10, []));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);

        foreach (var assignmentId in new[] { 1_000, 1_001, 1_002 })
        {
            var assignment = await database.GetAssignmentAsync(assignmentId);
            Assert.False(assignment.IsActive);
            Assert.True(assignment.IsDeleted);
        }

        var historicalAssignment = await database.GetAssignmentAsync(1_003);
        Assert.True(historicalAssignment.IsDeleted);
    }

    [Fact]
    public async Task Upsert_RepeatedIdenticalRequest_DoesNotUpdateAuditFields()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var request = new StoreContainerUpsertRequest(10, [100]);

        var firstResult = await service.UpsertAsync(request);
        var beforeRepeat = await database.GetAssignmentAsync(1_000);
        var secondResult = await service.UpsertAsync(request);
        var afterRepeat = await database.GetAssignmentAsync(1_000);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Null(beforeRepeat.UpdatedOn);
        Assert.Equal(beforeRepeat.UpdatedOn, afterRepeat.UpdatedOn);
        Assert.Equal(beforeRepeat.UpdatedById, afterRepeat.UpdatedById);
        Assert.Equal(beforeRepeat.UpdatedByPc, afterRepeat.UpdatedByPc);
    }

    [Fact]
    public async Task Upsert_ReactivatesExistingNonDeletedAssignment()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpsertAsync(
            new StoreContainerUpsertRequest(10, [100, 101]));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value, assignment => assignment.Id == 1_001);

        var assignment = await database.GetAssignmentAsync(1_001);
        Assert.True(assignment.IsActive);
        Assert.False(assignment.IsDeleted);
        Assert.Equal(
            1,
            await database.CountAssignmentsAsync(10, 101));
    }

    [Fact]
    public async Task Upsert_DoesNotRestoreSoftDeletedHistory()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpsertAsync(
            new StoreContainerUpsertRequest(10, [100, 103]));

        Assert.True(result.IsSuccess);
        var currentAssignment = Assert.Single(
            result.Value,
            assignment => assignment.ContainerId == 103);
        Assert.NotEqual(1_003, currentAssignment.Id);

        var historicalAssignment = await database.GetAssignmentAsync(1_003);
        Assert.True(historicalAssignment.IsDeleted);
        Assert.Equal(
            2,
            await database.CountAssignmentsAsync(10, 103));
    }

    [Fact]
    public async Task Upsert_RejectsAnotherCompanyStore()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpsertAsync(
            new StoreContainerUpsertRequest(20, [100]));

        Assert.True(result.IsFailure);
        Assert.Equal("StoreContainers.StoreNotFound", result.Error.Code);
    }

    [Fact]
    public async Task Upsert_RejectsAnotherCompanyContainer()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpsertAsync(
            new StoreContainerUpsertRequest(10, [200]));

        Assert.True(result.IsFailure);
        Assert.Equal("StoreContainers.ContainerNotFound", result.Error.Code);
    }

    [Fact]
    public async Task Upsert_RejectsInactiveContainerWithoutChangingAssignments()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpsertAsync(
            new StoreContainerUpsertRequest(10, [102]));

        Assert.True(result.IsFailure);
        Assert.Equal("StoreContainers.ContainerInactive", result.Error.Code);

        var assignment = await database.GetAssignmentAsync(1_000);
        Assert.True(assignment.IsActive);
        Assert.False(assignment.IsDeleted);
    }

    [Fact]
    public async Task Upsert_EmptySet_CanClearInactiveStore()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpsertAsync(
            new StoreContainerUpsertRequest(11, []));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);

        var assignment = await database.GetAssignmentAsync(1_010);
        Assert.False(assignment.IsActive);
        Assert.True(assignment.IsDeleted);
    }

    [Fact]
    public async Task Upsert_NonEmptySet_RejectsInactiveStore()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.UpsertAsync(
            new StoreContainerUpsertRequest(11, [100]));

        Assert.True(result.IsFailure);
        Assert.Equal("StoreContainers.StoreInactive", result.Error.Code);
    }

    [Fact]
    public async Task Upsert_DatabaseFailure_RollsBackCompleteReplacement()
    {
        await using var database =
            await StoreContainerTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        await database.FailInsertsForContainerAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.UpsertAsync(
                new StoreContainerUpsertRequest(10, [103, 105])));

        Assert.Equal(
            (IsActive: true, IsDeleted: false),
            await database.GetRawAssignmentStateAsync(1_000));
        Assert.Equal(
            (IsActive: true, IsDeleted: false),
            await database.GetRawAssignmentStateAsync(1_002));
        Assert.Equal(
            0,
            await database.CountCurrentAssignmentsAsync(10, 103));
        Assert.Equal(
            0,
            await database.CountCurrentAssignmentsAsync(10, 105));
    }

    private sealed class StoreContainerTestDatabase : IAsyncDisposable
    {
        private StoreContainerTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        private ApplicationDbContext Context { get; }

        public static async Task<StoreContainerTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection(
                "Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();

            var auditInterceptor = new AuditableEntityInterceptor(
                new HttpContextAccessor(),
                TimeProvider.System);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(auditInterceptor)
                .Options;
            var context = new ApplicationDbContext(options);

            await CreateSchemaAsync(context);
            await SeedAsync(context);

            return new StoreContainerTestDatabase(connection, context);
        }

        public StoreContainerService CreateService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public Task<StoreContainer> GetAssignmentAsync(int id) =>
            Context.StoreContainers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(assignment => assignment.Id == id);

        public Task<int> CountAssignmentsAsync(
            int storeId,
            int containerId) =>
            Context.StoreContainers
                .IgnoreQueryFilters()
                .CountAsync(assignment =>
                    assignment.StoreId == storeId &&
                    assignment.ContainerId == containerId);

        public async Task<int> CountCurrentAssignmentsAsync(
            int storeId,
            int containerId)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*)
                FROM StoreContainers
                WHERE StoreId = $storeId
                  AND ContainerId = $containerId
                  AND IsActive = 1
                  AND IsDeleted = 0;
                """;
            command.Parameters.AddWithValue("$storeId", storeId);
            command.Parameters.AddWithValue("$containerId", containerId);

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async Task<(bool IsActive, bool IsDeleted)>
            GetRawAssignmentStateAsync(int id)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText =
                """
                SELECT IsActive, IsDeleted
                FROM StoreContainers
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", id);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return (
                reader.GetInt64(0) == 1,
                reader.GetInt64(1) == 1);
        }

        public Task FailInsertsForContainerAsync() =>
            Context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER FailStoreContainerInsert
                BEFORE INSERT ON StoreContainers
                WHEN NEW.ContainerId = 105
                BEGIN
                    SELECT RAISE(ABORT, 'forced store-container failure');
                END;
                """);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static Task CreateSchemaAsync(ApplicationDbContext context) =>
            context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE BusinessPartners (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    PhoneNumber TEXT NULL,
                    Email TEXT NULL,
                    Address TEXT NULL,
                    TaxNumber TEXT NULL,
                    Currency INTEGER NOT NULL,
                    CreditLimit NUMERIC NOT NULL,
                    IsActive INTEGER NOT NULL,
                    Special INTEGER NOT NULL DEFAULT 0,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL,
                    UNIQUE (CompanyId, Id)
                );

                CREATE TABLE Stores (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Address TEXT NULL,
                    IsContainerStore INTEGER NOT NULL,
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
                    IsDeleted INTEGER NOT NULL,
                    UNIQUE (CompanyId, Id),
                    FOREIGN KEY (CompanyId, BusinessPartnerId)
                        REFERENCES BusinessPartners (CompanyId, Id)
                        ON DELETE RESTRICT
                );

                CREATE TABLE Containers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
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
                    IsDeleted INTEGER NOT NULL,
                    UNIQUE (CompanyId, Id)
                );

                CREATE TABLE StoreContainers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    ContainerId INTEGER NOT NULL,
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
                    IsDeleted INTEGER NOT NULL,
                    FOREIGN KEY (CompanyId, StoreId)
                        REFERENCES Stores (CompanyId, Id)
                        ON DELETE RESTRICT,
                    FOREIGN KEY (CompanyId, ContainerId)
                        REFERENCES Containers (CompanyId, Id)
                        ON DELETE RESTRICT
                );

                CREATE UNIQUE INDEX
                    UX_StoreContainers_CompanyId_StoreId_ContainerId_Active
                ON StoreContainers (CompanyId, StoreId, ContainerId)
                WHERE IsActive = 1 AND IsDeleted = 0;
                """);

        private static Task SeedAsync(ApplicationDbContext context) =>
            context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO BusinessPartners (
                    Id, CompanyId, Code, Name, Currency, CreditLimit,
                    IsActive, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 'BP-1', 'Active Partner', 1, 0,
                     1, 'test', '2026-01-01', 'test', 0),
                    (2, 1, 'BP-2', 'Inactive Partner', 1, 0,
                     0, 'test', '2026-01-01', 'test', 0),
                    (3, 2, 'BP-3', 'Other Company Partner', 1, 0,
                     1, 'test', '2026-01-01', 'test', 0);

                INSERT INTO Stores (
                    Id, CompanyId, BusinessPartnerId, Code, Name, Address,
                    IsContainerStore, IsActive, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES
                    (10, 1, 1, 'STORE-10', 'Active Container Store', NULL,
                     1, 1, 'test', '2026-01-01', 'test', 0),
                    (11, 1, 1, 'STORE-11', 'Inactive Container Store', NULL,
                     1, 0, 'test', '2026-01-01', 'test', 0),
                    (12, 1, 2, 'STORE-12', 'Inactive Partner Store', NULL,
                     1, 1, 'test', '2026-01-01', 'test', 0),
                    (13, 1, NULL, 'STORE-13', 'Product Store', NULL,
                     0, 1, 'test', '2026-01-01', 'test', 0),
                    (20, 2, 3, 'STORE-20', 'Other Company Store', NULL,
                     1, 1, 'test', '2026-01-01', 'test', 0);

                INSERT INTO Containers (
                    Id, CompanyId, Code, Name, Description, IsActive,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (100, 1, 'C-100', 'Current Container', NULL, 1,
                     'test', '2026-01-01', 'test', 0),
                    (101, 1, 'C-101', 'Available Container', NULL, 1,
                     'test', '2026-01-01', 'test', 0),
                    (102, 1, 'C-102', 'Inactive Assigned Container', NULL, 0,
                     'test', '2026-01-01', 'test', 0),
                    (103, 1, 'C-103', 'Historical Container', NULL, 1,
                     'test', '2026-01-01', 'test', 0),
                    (104, 1, 'C-104', 'Inactive Unassigned Container', NULL, 0,
                     'test', '2026-01-01', 'test', 0),
                    (105, 1, 'C-105', 'Failing Container', NULL, 1,
                     'test', '2026-01-01', 'test', 0),
                    (200, 2, 'C-200', 'Other Company Container', NULL, 1,
                     'test', '2026-01-01', 'test', 0);

                INSERT INTO StoreContainers (
                    Id, CompanyId, StoreId, ContainerId, IsActive,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1000, 1, 10, 100, 1,
                     'test', '2026-01-01', 'test', 0),
                    (1001, 1, 10, 101, 0,
                     'test', '2026-01-01', 'test', 0),
                    (1002, 1, 10, 102, 1,
                     'test', '2026-01-01', 'test', 0),
                    (1003, 1, 10, 103, 0,
                     'test', '2026-01-01', 'test', 1),
                    (1010, 1, 11, 100, 1,
                     'test', '2026-01-01', 'test', 0),
                    (2000, 2, 20, 200, 1,
                     'test', '2026-01-01', 'test', 0);
                """);
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
