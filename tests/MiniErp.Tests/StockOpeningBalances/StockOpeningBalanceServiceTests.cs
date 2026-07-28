using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.StockOpeningBalances;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Infrastructure.Services.StockOpeningBalances;

namespace MiniErp.Tests.StockOpeningBalances;

public sealed class StockOpeningBalanceServiceTests
{
    static StockOpeningBalanceServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task GetAll_ReturnsCompleteLineDetails()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var createResult = await service.AddAsync(CreateRequest());

        var result = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(createResult.Value.Id, item.Id);
        Assert.Equal(1, item.LineCount);
        var line = Assert.Single(item.Lines);
        Assert.Equal(1, line.ItemId);
        Assert.Equal(10, line.Count);
        Assert.Equal(2m, line.Weight);
        Assert.Equal(20m, line.Quantity);
        Assert.Equal(3m, line.Price);
        Assert.Equal(60m, line.Total);
    }

    [Fact]
    public async Task Add_NormalizesHeaderAndLineNotes()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new StockOpeningBalanceRequest(
                1,
                "  OPEN-TRIMMED  ",
                new DateOnly(2026, 1, 1),
                [
                    new StockOpeningBalanceLineRequest(
                        1,
                        10,
                        2m,
                        3m,
                        "   ")
                ],
                "   "));

        Assert.True(result.IsSuccess);
        Assert.Equal("OPEN-TRIMMED", result.Value.DocumentNumber);
        Assert.Null(result.Value.Notes);
        Assert.Null(result.Value.Lines.Single().Notes);
    }

    [Fact]
    public async Task OtherCompany_CannotReadUpdateOrDeleteOpeningBalance()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var companyOneService = database.CreateService(companyId: 1);
        var companyTwoService = database.CreateService(companyId: 2);
        var openingBalance = (await companyOneService.AddAsync(CreateRequest())).Value;

        var getResult = await companyTwoService.GetByIdAsync(openingBalance.Id);
        var updateResult = await companyTwoService.UpdateAsync(
            openingBalance.Id,
            new StockOpeningBalanceUpdateRequest(
                openingBalance.StoreId,
                openingBalance.DocumentNumber,
                openingBalance.DocumentDate,
                openingBalance.Lines
                    .Select(line => new StockOpeningBalanceLineRequest(
                        line.ItemId,
                        line.Count,
                        line.Weight,
                        line.Price,
                        line.Notes))
                    .ToArray(),
                openingBalance.Notes,
                openingBalance.RowVersion));
        var deleteResult = await companyTwoService.DeleteAsync(openingBalance.Id);

        Assert.Equal("StockOpeningBalances.NotFound", getResult.Error.Code);
        Assert.Equal("StockOpeningBalances.NotFound", updateResult.Error.Code);
        Assert.Equal("StockOpeningBalances.NotFound", deleteResult.Error.Code);
        Assert.True(
            (await companyOneService.GetByIdAsync(openingBalance.Id)).IsSuccess);
    }

    [Fact]
    public async Task Update_AllowsOwnNormalizedDocumentNumberAndLineNotes()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var openingBalance = (await service.AddAsync(CreateRequest())).Value;

        var result = await service.UpdateAsync(
            openingBalance.Id,
            new StockOpeningBalanceUpdateRequest(
                openingBalance.StoreId,
                $"  {openingBalance.DocumentNumber}  ",
                openingBalance.DocumentDate,
                [
                    new StockOpeningBalanceLineRequest(
                        1,
                        10,
                        2m,
                        3m,
                        "  Updated line note  ")
                ],
                "  Updated header note  ",
                openingBalance.RowVersion));

        Assert.True(result.IsSuccess);
        Assert.Equal("OPEN-001", result.Value.DocumentNumber);
        Assert.Equal("Updated header note", result.Value.Notes);
        Assert.Equal("Updated line note", result.Value.Lines.Single().Notes);
    }

    [Fact]
    public async Task Update_RejectsMissingRowVersion()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var openingBalance = (await service.AddAsync(CreateRequest())).Value;

        var result = await service.UpdateAsync(
            openingBalance.Id,
            new StockOpeningBalanceUpdateRequest(
                openingBalance.StoreId,
                openingBalance.DocumentNumber,
                openingBalance.DocumentDate,
                [
                    new StockOpeningBalanceLineRequest(
                        1,
                        10,
                        2m,
                        3m,
                        null)
                ],
                openingBalance.Notes,
                null));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "StockOpeningBalances.RowVersionRequired",
            result.Error.Code);
    }

    [Fact]
    public async Task RequestValidators_AcceptNormalizedMaximumLengths()
    {
        var documentNumber =
            $"  {new string('D', StockOpeningBalanceRequest.DocumentNumberMaximumLength)}  ";
        var notes =
            $"  {new string('N', StockOpeningBalanceRequest.NotesMaximumLength)}  ";
        var lines = new[]
        {
            new StockOpeningBalanceLineRequest(
                1,
                1,
                1m,
                1m,
                notes)
        };
        var createValidator = new StockOpeningBalanceRequestValidator();
        var updateValidator = new StockOpeningBalanceUpdateRequestValidator();

        var createResult = await createValidator.ValidateAsync(
            new StockOpeningBalanceRequest(
                1,
                documentNumber,
                new DateOnly(2026, 1, 1),
                lines,
                notes));
        var updateResult = await updateValidator.ValidateAsync(
            new StockOpeningBalanceUpdateRequest(
                1,
                documentNumber,
                new DateOnly(2026, 1, 1),
                lines,
                notes,
                new byte[8]));

        Assert.True(createResult.IsValid);
        Assert.True(updateResult.IsValid);
    }

    [Fact]
    public async Task RequestValidators_ReturnOneRequiredErrorForWhitespaceDocumentNumber()
    {
        var lines = new[]
        {
            new StockOpeningBalanceLineRequest(1, 1, 1m, 1m, null)
        };
        var createValidator = new StockOpeningBalanceRequestValidator();
        var updateValidator = new StockOpeningBalanceUpdateRequestValidator();

        var createResult = await createValidator.ValidateAsync(
            new StockOpeningBalanceRequest(
                1,
                "   ",
                new DateOnly(2026, 1, 1),
                lines,
                null));
        var updateResult = await updateValidator.ValidateAsync(
            new StockOpeningBalanceUpdateRequest(
                1,
                "   ",
                new DateOnly(2026, 1, 1),
                lines,
                null,
                new byte[8]));

        var createError = Assert.Single(createResult.Errors);
        var updateError = Assert.Single(updateResult.Errors);
        Assert.Equal("NotEmptyValidator", createError.ErrorCode);
        Assert.Equal("NotEmptyValidator", updateError.ErrorCode);
    }

    [Fact]
    public async Task Update_LineOnlyChange_AdvancesRowVersionAndRejectsStaleToken()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var createResult = await service.AddAsync(CreateRequest());
        var original = createResult.Value;
        Assert.Equal(20m, original.Lines.Single().Quantity);
        Assert.Equal(60m, original.Lines.Single().Total);
        var updateRequest = new StockOpeningBalanceUpdateRequest(
            original.StoreId,
            original.DocumentNumber,
            original.DocumentDate,
            [
                new StockOpeningBalanceLineRequest(
                    1,
                    25,
                    2m,
                    3m,
                    "line-only change")
            ],
            original.Notes,
            original.RowVersion);

        var updateResult = await service.UpdateAsync(original.Id, updateRequest);

        Assert.True(updateResult.IsSuccess);
        Assert.False(original.RowVersion.SequenceEqual(
            updateResult.Value.RowVersion));
        var updatedLine = updateResult.Value.Lines.Single();
        Assert.Equal(25, updatedLine.Count);
        Assert.Equal(2m, updatedLine.Weight);
        Assert.Equal(50m, updatedLine.Quantity);
        Assert.Equal(3m, updatedLine.Price);
        Assert.Equal(150m, updatedLine.Total);

        var staleResult = await service.UpdateAsync(
            original.Id,
            updateRequest with
            {
                Lines =
                [
                    new StockOpeningBalanceLineRequest(
                        1,
                        30,
                        2m,
                        3m,
                        "stale overwrite")
                ]
            });

        Assert.True(staleResult.IsFailure);
        Assert.Equal(
            "StockOpeningBalances.Concurrency",
            staleResult.Error.Code);

        var persisted = await service.GetByIdAsync(original.Id);
        Assert.Equal(50m, persisted.Value.Lines.Single().Quantity);
        Assert.Equal(150m, persisted.Value.Lines.Single().Total);
    }

    [Fact]
    public async Task Update_RemoveThenReAddLine_WithSameContext_RestoresActiveLine()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var original = (await service.AddAsync(CreateRequest())).Value;

        var removeResult = await service.UpdateAsync(
            original.Id,
            new StockOpeningBalanceUpdateRequest(
                original.StoreId,
                original.DocumentNumber,
                original.DocumentDate,
                [
                    new StockOpeningBalanceLineRequest(
                        2,
                        5,
                        1m,
                        4m,
                        "replacement item")
                ],
                original.Notes,
                original.RowVersion));

        Assert.True(removeResult.IsSuccess);
        Assert.Equal(2, Assert.Single(removeResult.Value.Lines).ItemId);

        var trackedHeader = database.Context.ChangeTracker
            .Entries<StockOpeningBalance>()
            .Single()
            .Entity;
        await database.Context.Entry(trackedHeader).ReloadAsync();

        var reAddResult = await service.UpdateAsync(
            original.Id,
            new StockOpeningBalanceUpdateRequest(
                removeResult.Value.StoreId,
                removeResult.Value.DocumentNumber,
                removeResult.Value.DocumentDate,
                [
                    new StockOpeningBalanceLineRequest(
                        1,
                        3,
                        2m,
                        5m,
                        "re-added item")
                ],
                removeResult.Value.Notes,
                removeResult.Value.RowVersion));

        Assert.True(reAddResult.IsSuccess);
        var restoredLine = Assert.Single(reAddResult.Value.Lines);
        Assert.Equal(1, restoredLine.ItemId);
        Assert.Equal(6m, restoredLine.Quantity);
        Assert.Equal(30m, restoredLine.Total);
        Assert.Equal(
            1,
            await database.Context.StockOpeningBalanceLines.CountAsync());
    }

    [Fact]
    public async Task Update_DatabaseConcurrencyConflict_ReturnsConflictAndClearsTracking()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var original = (await service.AddAsync(CreateRequest())).Value;

        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE StockOpeningBalances SET Notes = Notes WHERE Id = {original.Id}");

        var result = await service.UpdateAsync(
            original.Id,
            new StockOpeningBalanceUpdateRequest(
                original.StoreId,
                original.DocumentNumber,
                original.DocumentDate,
                [
                    new StockOpeningBalanceLineRequest(
                        1,
                        25,
                        2m,
                        3m,
                        "concurrent overwrite")
                ],
                "concurrent overwrite",
                original.RowVersion));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "StockOpeningBalances.Concurrency",
            result.Error.Code);
        Assert.Empty(
            database.Context.ChangeTracker
                .Entries<StockOpeningBalance>());
        Assert.Empty(
            database.Context.ChangeTracker
                .Entries<StockOpeningBalanceLine>());

        var persisted = await database.Context.StockOpeningBalances
            .AsNoTracking()
            .Include(balance => balance.Lines)
            .SingleAsync(balance => balance.Id == original.Id);
        Assert.False(persisted.IsDeleted);
        Assert.Equal(original.Notes, persisted.Notes);
        Assert.Equal(20m, Assert.Single(persisted.Lines).Quantity);
    }

    [Fact]
    public async Task Delete_DatabaseConcurrencyConflict_ReturnsConflictAndRollsBack()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var original = (await service.AddAsync(CreateRequest())).Value;

        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE StockOpeningBalances SET Notes = Notes WHERE Id = {original.Id}");

        var result = await service.DeleteAsync(original.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "StockOpeningBalances.Concurrency",
            result.Error.Code);
        Assert.Empty(
            database.Context.ChangeTracker
                .Entries<StockOpeningBalance>());
        Assert.Empty(
            database.Context.ChangeTracker
                .Entries<StockOpeningBalanceLine>());

        var persistedHeader = await database.Context.StockOpeningBalances
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(balance => balance.Id == original.Id);
        var persistedLine = await database.Context.StockOpeningBalanceLines
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(line =>
                line.StockOpeningBalanceId == original.Id);
        Assert.False(persistedHeader.IsDeleted);
        Assert.False(persistedLine.IsDeleted);
    }

    [Fact]
    public async Task Add_RejectsContainerStore()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            CreateRequest(storeId: 2));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "StockOpeningBalances.ContainerStoreNotAllowed",
            result.Error.Code);
        Assert.Equal(
            0,
            await database.Context.StockOpeningBalances.CountAsync());
    }

    [Fact]
    public async Task GetById_ReturnsNullableItemUnit()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var createResult = await service.AddAsync(CreateRequest());
        var lineId = createResult.Value.Lines.Single().Id;
        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE StockOpeningBalanceLines SET ItemUnitId = NULL WHERE Id = {lineId}");
        database.Context.ChangeTracker.Clear();

        var result = await service.GetByIdAsync(createResult.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Lines.Single().ItemUnitId);
        Assert.Null(result.Value.Lines.Single().ItemUnitName);
    }

    [Fact]
    public async Task Add_RejectsStoreAndItemFromAnotherCompany()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var storeResult = await service.AddAsync(
            CreateRequest(storeId: 3));
        var itemResult = await service.AddAsync(
            CreateRequest(
                lines:
                [
                    new StockOpeningBalanceLineRequest(
                        3,
                        10,
                        1m,
                        2m,
                        null)
                ]));

        Assert.Equal(
            "StockOpeningBalances.StoreNotFound",
            storeResult.Error.Code);
        Assert.Equal(
            "StockOpeningBalances.ItemNotFound",
            itemResult.Error.Code);
        Assert.Equal(
            0,
            await database.Context.StockOpeningBalances.CountAsync());
    }

    [Fact]
    public async Task Add_WhenSecondLineWriteFails_RollsBackWholeAggregate()
    {
        await using var database = await StockOpeningBalanceTestDatabase.CreateAsync(
            addForcedLineFailureTrigger: true);
        var service = database.CreateService(companyId: 1);
        var request = CreateRequest(
            lines:
            [
                new StockOpeningBalanceLineRequest(1, 10, 1m, 2m, null),
                new StockOpeningBalanceLineRequest(2, 13, 1m, 2m, null)
            ]);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.AddAsync(request));

        database.Context.ChangeTracker.Clear();
        Assert.Equal(
            0,
            await database.Context.StockOpeningBalances
                .IgnoreQueryFilters()
                .CountAsync());
        Assert.Equal(
            0,
            await database.Context.StockOpeningBalanceLines
                .IgnoreQueryFilters()
                .CountAsync());
    }

    private static StockOpeningBalanceRequest CreateRequest(
        int storeId = 1,
        IReadOnlyList<StockOpeningBalanceLineRequest>? lines = null) =>
        new(
            storeId,
            "OPEN-001",
            new DateOnly(2026, 1, 1),
            lines ??
            [
                new StockOpeningBalanceLineRequest(
                    1,
                    10,
                    2m,
                    3m,
                    null)
            ],
            "opening stock");

    private sealed class StockOpeningBalanceTestDatabase : IAsyncDisposable
    {
        private StockOpeningBalanceTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        public ApplicationDbContext Context { get; }

        public static async Task<StockOpeningBalanceTestDatabase> CreateAsync(
            bool addForcedLineFailureTrigger = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
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
            await SeedReferenceDataAsync(context);

            if (addForcedLineFailureTrigger)
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TRIGGER AbortSelectedOpeningBalanceLine
                    BEFORE INSERT ON StockOpeningBalanceLines
                    WHEN NEW.Quantity = 13
                    BEGIN
                        SELECT RAISE(ABORT, 'forced line failure');
                    END;
                    """);
            }

            return new StockOpeningBalanceTestDatabase(connection, context);
        }

        public StockOpeningBalanceService CreateService(int companyId) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId));

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static async Task CreateSchemaAsync(ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE Companies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Address TEXT NOT NULL,
                    CommercialRegister TEXT NOT NULL,
                    TaxNumber TEXT NOT NULL,
                    ManagerName TEXT NOT NULL,
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
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE ItemUnits (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
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

                CREATE TABLE Items (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    ItemUnitId INTEGER NOT NULL,
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
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StockOpeningBalances (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    DocumentNumber TEXT NOT NULL,
                    DocumentDate TEXT NOT NULL,
                    Notes TEXT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
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

                CREATE UNIQUE INDEX UX_StockOpeningBalances_Company_Document
                ON StockOpeningBalances (CompanyId, DocumentNumber)
                WHERE IsDeleted = 0;

                CREATE TABLE StockOpeningBalanceLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    StockOpeningBalanceId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    ItemUnitId INTEGER NULL,
                    Count INTEGER NOT NULL CHECK (Count > 0),
                    Weight NUMERIC NOT NULL CHECK (Weight > 0),
                    Quantity NUMERIC NOT NULL CHECK (Quantity > 0),
                    Price NUMERIC NOT NULL CHECK (Price >= 0),
                    Total NUMERIC NOT NULL CHECK (Total >= 0),
                    Notes TEXT NULL,
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

                CREATE UNIQUE INDEX UX_StockOpeningBalanceLines_Company_Document_Item
                ON StockOpeningBalanceLines (
                    CompanyId,
                    StockOpeningBalanceId,
                    ItemId)
                WHERE IsDeleted = 0;

                CREATE TRIGGER AdvanceStockOpeningBalanceRowVersion
                AFTER UPDATE ON StockOpeningBalances
                BEGIN
                    UPDATE StockOpeningBalances
                    SET RowVersion = randomblob(8)
                    WHERE Id = NEW.Id;
                END;
                """);
        }

        private static async Task SeedReferenceDataAsync(
            ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Companies (
                    Id, Name, Address, CommercialRegister, TaxNumber,
                    ManagerName, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 'Company A', '', 'CR-A', 'TX-A', 'Manager',
                     'test', '2026-01-01', 'test', 0),
                    (2, 'Company B', '', 'CR-B', 'TX-B', 'Manager',
                     'test', '2026-01-01', 'test', 0);

                INSERT INTO Stores (
                    Id, CompanyId, BusinessPartnerId, Code, Name, Address,
                    IsContainerStore, IsActive, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, NULL, 'MAIN', 'Main Store', NULL,
                     0, 1, 'test', '2026-01-01', 'test', 0),
                    (2, 1, 100, 'CONT', 'Container Store', NULL,
                     1, 1, 'test', '2026-01-01', 'test', 0),
                    (3, 2, NULL, 'OTHER', 'Other Company Store', NULL,
                     0, 1, 'test', '2026-01-01', 'test', 0);

                INSERT INTO ItemUnits (
                    Id, CompanyId, Name, IsActive, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 'Piece', 1, 'test', '2026-01-01', 'test', 0),
                    (2, 2, 'Piece', 1, 'test', '2026-01-01', 'test', 0);

                INSERT INTO Items (
                    Id, CompanyId, ItemUnitId, Code, Name, Description,
                    IsActive, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 1, 'ITEM-1', 'Item One', NULL,
                     1, 'test', '2026-01-01', 'test', 0),
                    (2, 1, 1, 'ITEM-2', 'Item Two', NULL,
                     1, 'test', '2026-01-01', 'test', 0),
                    (3, 2, 2, 'ITEM-3', 'Other Company Item', NULL,
                     1, 'test', '2026-01-01', 'test', 0);
                """);
        }
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
