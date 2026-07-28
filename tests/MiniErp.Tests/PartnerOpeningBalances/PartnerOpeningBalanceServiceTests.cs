using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.PartnerOpeningBalances;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Infrastructure.Services.PartnerOpeningBalances;

namespace MiniErp.Tests.PartnerOpeningBalances;

public sealed class PartnerOpeningBalanceServiceTests
{
    static PartnerOpeningBalanceServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task GetAll_ReturnsCompleteDetailFields()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var createResult = await service.AddAsync(CreateRequest());
        var result = await service.GetAllAsync(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20
            });

        Assert.True(createResult.IsSuccess);
        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(createResult.Value.Id, item.Id);
        Assert.Equal(1, item.CompanyId);
        Assert.Equal(1, item.BusinessPartnerId);
        Assert.Equal("Company A Partner", item.BusinessPartnerName);
        Assert.Equal("OPEN-001", item.DocumentNumber);
        Assert.Equal(new DateOnly(2026, 1, 1), item.DocumentDate);
        Assert.Equal(CurrencyCode.EGP, item.Currency);
        Assert.Equal(PartnerBalanceType.Receivable, item.BalanceType);
        Assert.Equal(125.50m, item.Amount);
        Assert.Equal("Opening balance", item.Notes);
        Assert.NotEmpty(item.RowVersion);
    }

    [Fact]
    public async Task Add_RejectsCrossCompanyPartnerAndCurrencyMismatch()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var crossCompanyResult = await service.AddAsync(
            CreateRequest(businessPartnerId: 3));
        var currencyMismatchResult = await service.AddAsync(
            CreateRequest(
                documentNumber: "OPEN-002",
                currency: CurrencyCode.USD));

        Assert.Equal(
            "PartnerOpeningBalances.BusinessPartnerNotFound",
            crossCompanyResult.Error.Code);
        Assert.Equal(
            "PartnerOpeningBalances.CurrencyMismatch",
            currencyMismatchResult.Error.Code);
        Assert.Equal(
            0,
            await database.Context.PartnerOpeningBalances.CountAsync());
    }

    [Fact]
    public async Task Add_RejectsInactivePartnerAndDuplicateNormalizedDocumentNumber()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var createResult = await service.AddAsync(CreateRequest());

        var inactiveResult = await service.AddAsync(
            CreateRequest(
                businessPartnerId: 4,
                documentNumber: "OPEN-002"));
        var duplicateResult = await service.AddAsync(
            CreateRequest(documentNumber: "  OPEN-001  "));

        Assert.True(createResult.IsSuccess);
        Assert.Equal(
            "PartnerOpeningBalances.BusinessPartnerInactive",
            inactiveResult.Error.Code);
        Assert.Equal(
            "PartnerOpeningBalances.DocumentNumberExists",
            duplicateResult.Error.Code);
    }

    [Fact]
    public async Task Add_NormalizesDocumentNumberAndBlankNotes()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new PartnerOpeningBalanceRequest(
                1,
                "  OPEN-TRIMMED  ",
                new DateOnly(2026, 1, 1),
                CurrencyCode.EGP,
                PartnerBalanceType.Receivable,
                125.50m,
                "   "));

        Assert.True(result.IsSuccess);
        Assert.Equal("OPEN-TRIMMED", result.Value.DocumentNumber);
        Assert.Null(result.Value.Notes);
    }

    [Fact]
    public async Task OtherCompany_CannotReadUpdateOrDeleteOpeningBalance()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var companyOneService = database.CreateService(companyId: 1);
        var companyTwoService = database.CreateService(companyId: 2);
        var createResult = await companyOneService.AddAsync(CreateRequest());
        var openingBalance = createResult.Value;

        var getResult = await companyTwoService.GetByIdAsync(openingBalance.Id);
        var updateResult = await companyTwoService.UpdateAsync(
            openingBalance.Id,
            new PartnerOpeningBalanceUpdateRequest(
                openingBalance.BusinessPartnerId,
                openingBalance.DocumentNumber,
                openingBalance.DocumentDate,
                openingBalance.Currency,
                openingBalance.BalanceType,
                openingBalance.Amount,
                openingBalance.Notes,
                openingBalance.RowVersion));
        var deleteResult = await companyTwoService.DeleteAsync(openingBalance.Id);

        Assert.Equal("PartnerOpeningBalances.NotFound", getResult.Error.Code);
        Assert.Equal("PartnerOpeningBalances.NotFound", updateResult.Error.Code);
        Assert.Equal("PartnerOpeningBalances.NotFound", deleteResult.Error.Code);
        Assert.True(
            (await companyOneService.GetByIdAsync(openingBalance.Id)).IsSuccess);
    }

    [Fact]
    public async Task Update_AllowsOwnNormalizedDocumentNumber()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var createResult = await service.AddAsync(CreateRequest());
        var openingBalance = createResult.Value;

        var result = await service.UpdateAsync(
            openingBalance.Id,
            new PartnerOpeningBalanceUpdateRequest(
                openingBalance.BusinessPartnerId,
                $"  {openingBalance.DocumentNumber}  ",
                openingBalance.DocumentDate,
                openingBalance.Currency,
                openingBalance.BalanceType,
                openingBalance.Amount,
                "  Updated notes  ",
                openingBalance.RowVersion));

        Assert.True(result.IsSuccess);
        Assert.Equal("OPEN-001", result.Value.DocumentNumber);
        Assert.Equal("Updated notes", result.Value.Notes);
    }

    [Fact]
    public async Task Update_RejectsMissingRowVersion()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var createResult = await service.AddAsync(CreateRequest());
        var openingBalance = createResult.Value;

        var result = await service.UpdateAsync(
            openingBalance.Id,
            new PartnerOpeningBalanceUpdateRequest(
                openingBalance.BusinessPartnerId,
                openingBalance.DocumentNumber,
                openingBalance.DocumentDate,
                openingBalance.Currency,
                openingBalance.BalanceType,
                openingBalance.Amount,
                openingBalance.Notes,
                null));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PartnerOpeningBalances.RowVersionRequired",
            result.Error.Code);
    }

    [Fact]
    public async Task RequestValidators_AcceptNormalizedMaximumLengths()
    {
        var documentNumber =
            $"  {new string('D', PartnerOpeningBalanceRequest.DocumentNumberMaximumLength)}  ";
        var notes =
            $"  {new string('N', PartnerOpeningBalanceRequest.NotesMaximumLength)}  ";
        var createValidator = new PartnerOpeningBalanceRequestValidator();
        var updateValidator = new PartnerOpeningBalanceUpdateRequestValidator();

        var createResult = await createValidator.ValidateAsync(
            new PartnerOpeningBalanceRequest(
                1,
                documentNumber,
                new DateOnly(2026, 1, 1),
                CurrencyCode.EGP,
                PartnerBalanceType.Receivable,
                1m,
                notes));
        var updateResult = await updateValidator.ValidateAsync(
            new PartnerOpeningBalanceUpdateRequest(
                1,
                documentNumber,
                new DateOnly(2026, 1, 1),
                CurrencyCode.EGP,
                PartnerBalanceType.Receivable,
                1m,
                notes,
                new byte[8]));

        Assert.True(createResult.IsValid);
        Assert.True(updateResult.IsValid);
    }

    [Fact]
    public async Task RequestValidators_ReturnOneRequiredErrorForWhitespaceDocumentNumber()
    {
        var createValidator = new PartnerOpeningBalanceRequestValidator();
        var updateValidator = new PartnerOpeningBalanceUpdateRequestValidator();

        var createResult = await createValidator.ValidateAsync(
            new PartnerOpeningBalanceRequest(
                1,
                "   ",
                new DateOnly(2026, 1, 1),
                CurrencyCode.EGP,
                PartnerBalanceType.Receivable,
                1m,
                null));
        var updateResult = await updateValidator.ValidateAsync(
            new PartnerOpeningBalanceUpdateRequest(
                1,
                "   ",
                new DateOnly(2026, 1, 1),
                CurrencyCode.EGP,
                PartnerBalanceType.Receivable,
                1m,
                null,
                new byte[8]));

        var createError = Assert.Single(createResult.Errors);
        var updateError = Assert.Single(updateResult.Errors);
        Assert.Equal("NotEmptyValidator", createError.ErrorCode);
        Assert.Equal("NotEmptyValidator", updateError.ErrorCode);
    }

    [Fact]
    public async Task Update_UsesOriginalClientTokenAndRejectsStaleToken()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var createResult = await service.AddAsync(CreateRequest());
        var original = createResult.Value;

        var updateResult = await service.UpdateAsync(
            original.Id,
            new PartnerOpeningBalanceUpdateRequest(
                original.BusinessPartnerId,
                "OPEN-UPDATED",
                original.DocumentDate,
                original.Currency,
                PartnerBalanceType.Payable,
                250.75m,
                "Updated opening balance",
                original.RowVersion));

        var staleResult = await service.UpdateAsync(
            original.Id,
            new PartnerOpeningBalanceUpdateRequest(
                original.BusinessPartnerId,
                "OPEN-STALE",
                original.DocumentDate,
                original.Currency,
                original.BalanceType,
                original.Amount,
                original.Notes,
                original.RowVersion));

        Assert.True(updateResult.IsSuccess);
        Assert.False(original.RowVersion.SequenceEqual(updateResult.Value.RowVersion));
        Assert.Equal(PartnerBalanceType.Payable, updateResult.Value.BalanceType);
        Assert.Equal(250.75m, updateResult.Value.Amount);
        Assert.True(staleResult.IsFailure);
        Assert.Equal(
            "PartnerOpeningBalances.Concurrency",
            staleResult.Error.Code);

        var persisted = await service.GetByIdAsync(original.Id);
        Assert.Equal("OPEN-UPDATED", persisted.Value.DocumentNumber);
        Assert.Equal(250.75m, persisted.Value.Amount);
    }

    [Fact]
    public async Task Update_RejectsStaleTokenWhenValuesAreUnchangedAndClearsTracking()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var createResult = await service.AddAsync(CreateRequest());
        var original = createResult.Value;

        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE PartnerOpeningBalances SET Amount = Amount WHERE Id = {original.Id}");

        var result = await service.UpdateAsync(
            original.Id,
            new PartnerOpeningBalanceUpdateRequest(
                original.BusinessPartnerId,
                original.DocumentNumber,
                original.DocumentDate,
                original.Currency,
                original.BalanceType,
                original.Amount,
                original.Notes,
                original.RowVersion));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PartnerOpeningBalances.Concurrency",
            result.Error.Code);
        Assert.Empty(
            database.Context.ChangeTracker
                .Entries<PartnerOpeningBalance>());
    }

    [Fact]
    public async Task Delete_IsSoftDeleteAndExcludedFromNormalQueries()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var createResult = await service.AddAsync(CreateRequest());

        var deleteResult = await service.DeleteAsync(createResult.Value.Id);
        var getResult = await service.GetByIdAsync(createResult.Value.Id);
        var deleted = await database.Context.PartnerOpeningBalances
            .IgnoreQueryFilters()
            .SingleAsync();

        Assert.True(deleteResult.IsSuccess);
        Assert.True(getResult.IsFailure);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedOn);
    }

    [Fact]
    public async Task Delete_ConcurrencyFailureRollsBackAndClearsTracking()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var createResult = await service.AddAsync(CreateRequest());

        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE PartnerOpeningBalances SET Amount = Amount WHERE Id = {createResult.Value.Id}");

        var result = await service.DeleteAsync(createResult.Value.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PartnerOpeningBalances.Concurrency",
            result.Error.Code);
        Assert.Empty(
            database.Context.ChangeTracker
                .Entries<PartnerOpeningBalance>());

        var persisted = await database.Context.PartnerOpeningBalances
            .IgnoreQueryFilters()
            .SingleAsync();
        Assert.False(persisted.IsDeleted);
    }

    [Fact]
    public async Task Add_WhenInsertFails_DoesNotPersistOpeningBalance()
    {
        await using var database = await PartnerOpeningBalanceTestDatabase.CreateAsync(
            addForcedInsertFailureTrigger: true);
        var service = database.CreateService(companyId: 1);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.AddAsync(CreateRequest(amount: 13m)));

        database.Context.ChangeTracker.Clear();
        Assert.Equal(
            0,
            await database.Context.PartnerOpeningBalances
                .IgnoreQueryFilters()
                .CountAsync());
    }

    private static PartnerOpeningBalanceRequest CreateRequest(
        int businessPartnerId = 1,
        string documentNumber = "OPEN-001",
        CurrencyCode currency = CurrencyCode.EGP,
        PartnerBalanceType balanceType = PartnerBalanceType.Receivable,
        decimal amount = 125.50m) =>
        new(
            businessPartnerId,
            documentNumber,
            new DateOnly(2026, 1, 1),
            currency,
            balanceType,
            amount,
            "Opening balance");

    private sealed class PartnerOpeningBalanceTestDatabase : IAsyncDisposable
    {
        private PartnerOpeningBalanceTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        public ApplicationDbContext Context { get; }

        public static async Task<PartnerOpeningBalanceTestDatabase> CreateAsync(
            bool addForcedInsertFailureTrigger = false)
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

            if (addForcedInsertFailureTrigger)
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TRIGGER AbortPartnerOpeningBalanceInsert
                    BEFORE INSERT ON PartnerOpeningBalances
                    WHEN NEW.Amount = 13
                    BEGIN
                        SELECT RAISE(ABORT, 'forced partner opening balance failure');
                    END;
                    """);
            }

            return new PartnerOpeningBalanceTestDatabase(connection, context);
        }

        public PartnerOpeningBalanceService CreateService(int companyId) =>
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

                CREATE TABLE PartnerOpeningBalances (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NOT NULL,
                    DocumentNumber TEXT NOT NULL,
                    DocumentDate TEXT NOT NULL,
                    Currency INTEGER NOT NULL,
                    BalanceType INTEGER NOT NULL,
                    Amount NUMERIC NOT NULL CHECK (Amount > 0),
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

                CREATE UNIQUE INDEX UX_PartnerOpeningBalances_Company_Document
                ON PartnerOpeningBalances (CompanyId, DocumentNumber)
                WHERE IsDeleted = 0;

                CREATE TRIGGER AdvancePartnerOpeningBalanceRowVersion
                AFTER UPDATE ON PartnerOpeningBalances
                BEGIN
                    UPDATE PartnerOpeningBalances
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

                INSERT INTO BusinessPartners (
                    Id, CompanyId, Code, Name, Currency, CreditLimit, IsActive,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 'BP-1', 'Company A Partner', 1, 1000, 1,
                     'test', '2026-01-01', 'test', 0),
                    (2, 1, 'BP-2', 'Company A USD Partner', 2, 1000, 1,
                     'test', '2026-01-01', 'test', 0),
                    (3, 2, 'BP-3', 'Company B Partner', 1, 1000, 1,
                     'test', '2026-01-01', 'test', 0),
                    (4, 1, 'BP-4', 'Company A Inactive Partner', 1, 1000, 0,
                     'test', '2026-01-01', 'test', 0);
                """);
        }
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
