using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.DriverOpeningBalances;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Services.AccountMappings;
using MiniErp.Infrastructure.Services.DriverOpeningBalances;
using MiniErp.Infrastructure.Services.FiscalYears;
using MiniErp.Infrastructure.Services.JournalEntries;

namespace MiniErp.Tests.CashManagement;

public sealed class DriverOpeningBalanceServiceTests
{
    [Theory]
    [InlineData(DriverOpeningBalanceType.Receivable, 123.45, 0)]
    [InlineData(DriverOpeningBalanceType.Payable, 0, 123.45)]
    public async Task OpeningPostsOnlyToLedgerAndAnchorsDriverStatement(
        DriverOpeningBalanceType type, double debit, double credit)
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await SetUpAsync(db);
        var service = CreateService(db, 1);

        var created = await service.AddAsync(Request(type));

        Assert.True(created.IsSuccess, created.Error.Description);
        Assert.Equal(1, created.Value.DriverId);
        Assert.Equal("Driver One", created.Value.DriverName);
        Assert.Equal(1, created.Value.FiscalYearId);
        Assert.Equal("2026", created.Value.FiscalYearName);
        Assert.Equal(new DateOnly(2026, 1, 1), created.Value.DocumentDate);
        Assert.Equal(CurrencyCode.EGP, created.Value.Currency);
        Assert.Equal(123.45m, created.Value.Amount);
        Assert.Equal(type, created.Value.BalanceType);
        Assert.False(created.Value.IsCarriedForward);
        Assert.False(created.Value.IsReadOnly);
        Assert.NotEmpty(created.Value.RowVersion!);

        var entry = await db.Context.JournalEntries.Include(entry => entry.Lines)
            .SingleAsync(entry => entry.SourceType == JournalEntrySourceType.DriverOpeningBalance);
        Assert.Equal(JournalEntrySourceType.DriverOpeningBalance, entry.SourceType);
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.Equal(created.Value.Id, entry.SourceId);
        Assert.Equal(2, entry.Lines.Count);
        var driverLine = Assert.Single(entry.Lines, line => line.AccountId == 101);
        Assert.Equal(JournalPartyType.Driver, driverLine.PartyType);
        Assert.Equal(1, driverLine.PartyId);
        Assert.Equal((decimal)debit, driverLine.Debit);
        Assert.Equal((decimal)credit, driverLine.Credit);
        var equityLine = Assert.Single(entry.Lines, line => line.AccountId == 102);
        Assert.Null(equityLine.PartyType);
        Assert.Equal((decimal)credit, equityLine.Debit);
        Assert.Equal((decimal)debit, equityLine.Credit);

        var statement = await db.CreateStatementService(1).GetDriverStatementAsync(
            Page(), new DriverStatementFilterRequest(DriverId: 1, FiscalYearId: 1));
        Assert.True(statement.IsSuccess, statement.Error.Description);
        Assert.Equal(123.45m, statement.Value.Summary.OpeningBalanceAmount);
        Assert.Equal(0m, statement.Value.Summary.TotalPaidToDriver);
        Assert.Equal(0m, statement.Value.Summary.TotalReceivedFromDriver);
        var opening = Assert.Single(statement.Value.Items);
        Assert.True(opening.IsOpeningBalance);
        Assert.True(opening.IsReadOnly);
        Assert.Equal(0m, opening.AmountPaidToDriver);
        Assert.Equal(0m, opening.AmountReceivedFromDriver);
    }

    [Fact]
    public async Task UpdateAndDeleteRequireCurrentVersionAndMaintainOneJournal()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await SetUpAsync(db);
        var service = CreateService(db, 1);
        var created = await service.AddAsync(Request());
        Assert.True(created.IsSuccess, created.Error.Description);
        var original = created.Value;

        var duplicate = await service.AddAsync(Request());
        Assert.Equal("DriverOpeningBalances.Duplicate", duplicate.Error.Code);
        var change = new DriverOpeningBalanceUpdateRequest(
            DriverId: 2, FiscalYearId: 1, Amount: 20m,
            BalanceType: DriverOpeningBalanceType.Payable, Notes: null,
            RowVersion: original.RowVersion);
        var differentDriver = await service.UpdateAsync(original.Id, change);
        Assert.Equal("DriverOpeningBalances.ScopeImmutable", differentDriver.Error.Code);

        var updated = await service.UpdateAsync(original.Id, change with { DriverId = 1 });
        Assert.True(updated.IsSuccess, updated.Error.Description);
        Assert.Equal(original.DocumentNumber, updated.Value.DocumentNumber);
        Assert.Equal(20m, updated.Value.Amount);
        Assert.NotEqual(original.RowVersion, updated.Value.RowVersion);
        var entry = await db.Context.JournalEntries.AsNoTracking().Include(entry => entry.Lines)
            .SingleAsync(entry => entry.SourceType == JournalEntrySourceType.DriverOpeningBalance);
        Assert.Equal(20m, Assert.Single(entry.Lines, line => line.PartyType == JournalPartyType.Driver).Credit);

        var staleDelete = await service.DeleteAsync(original.Id, original.RowVersion);
        Assert.Equal("DriverOpeningBalances.Concurrency", staleDelete.Error.Code);
        Assert.True((await service.DeleteAsync(original.Id, updated.Value.RowVersion)).IsSuccess);
        Assert.False(await db.Context.JournalEntries.AnyAsync(entry => entry.SourceType == JournalEntrySourceType.DriverOpeningBalance));
        Assert.Empty((await service.GetAllAsync(Page(), 1, null)).Value.Items);
        Assert.True((await service.AddAsync(Request())).IsSuccess);
    }

    [Fact]
    public async Task MissingEquityMappingRollsBackOpeningAndAllowsRetry()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await SetUpAsync(db);
        await db.Context.Database.ExecuteSqlRawAsync("DELETE FROM AccountMappings WHERE MappingType = 17;");
        var service = CreateService(db, 1);

        Assert.True((await service.AddAsync(Request())).IsFailure);
        Assert.False(await db.Context.JournalEntries.AnyAsync(entry => entry.SourceType == JournalEntrySourceType.DriverOpeningBalance));
        Assert.Empty((await service.GetAllAsync(Page(), 1, null)).Value.Items);
        await db.Context.Database.ExecuteSqlRawAsync(
            "INSERT INTO AccountMappings (CompanyId, FiscalYearId, MappingType, AccountId, CreatedById, CreatedOn, CreatedByPc, IsDeleted) VALUES (1, 1, 17, 102, 'test', '2026-01-01', 'test', 0);");
        Assert.True((await service.AddAsync(Request())).IsSuccess);
    }

    [Fact]
    public async Task CarriedDriverLinesAreNettedAndReadOnlyWithinTheirFiscalYear()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await SetUpAsync(db);
        await db.ConfigureSeparateFiscalYearsAsync();
        await SeedOpeningJournalAsync(db, sourceType: 13);
        var service = CreateService(db, 1);

        var rows = await service.GetAllAsync(Page(), 100, 1);
        Assert.True(rows.IsSuccess, rows.Error.Description);
        var carry = Assert.Single(rows.Value.Items);
        Assert.True(carry.Id < 0);
        Assert.True(carry.IsCarriedForward);
        Assert.True(carry.IsReadOnly);
        Assert.Null(carry.RowVersion);
        Assert.Equal(1, carry.DriverId);
        Assert.Equal(100, carry.FiscalYearId);
        Assert.Equal("2026", carry.FiscalYearName);
        Assert.Equal("2025", carry.DocumentNumber);
        Assert.Equal(new DateOnly(2026, 1, 1), carry.DocumentDate);
        Assert.Equal(20m, carry.Amount);
        Assert.Equal(CurrencyCode.EGP, carry.Currency);
        Assert.Equal(DriverOpeningBalanceType.Receivable, carry.BalanceType);
        Assert.Equal("DriverOpeningBalances.ReadOnly", (await service.DeleteAsync(carry.Id, null)).Error.Code);
        Assert.Equal("DriverOpeningBalances.CarriedForwardExists",
            (await service.AddAsync(Request() with { FiscalYearId = 100 })).Error.Code);
        Assert.Equal("DriverOpeningBalances.FiscalYearClosed",
            (await service.AddAsync(Request())).Error.Code);
        Assert.Empty((await service.GetAllAsync(Page(), 1, 1)).Value.Items);
        Assert.True((await CreateService(db, 2).GetAllAsync(Page(), 100, 1)).IsFailure);
        Assert.True((await service.AddAsync(Request() with { DriverId = 3, FiscalYearId = 100 })).IsFailure);
    }

    [Fact]
    public async Task CarriedOpeningCannotBeDeletedThroughTheJournalEndpoint()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await SetUpAsync(db);
        await db.ConfigureSeparateFiscalYearsAsync();
        await SeedOpeningJournalAsync(db, sourceType: 13);
        var company = new TestCurrentCompanyContext(1);
        var journal = new JournalEntryService(db.Context, company, TimeProvider.System,
            new FiscalYearQueryScopeResolver(db.Context, company));
        var version = await db.Context.JournalEntries
            .Where(entry => entry.Id == 801)
            .Select(entry => entry.RowVersion)
            .SingleAsync();

        var deleted = await journal.DeleteAsync(801, version);

        Assert.Equal("JournalEntries.DriverCarryForwardReadOnly", deleted.Error.Code);
        Assert.True(await db.Context.JournalEntries.AnyAsync(entry => entry.Id == 801));
    }

    [Fact]
    public async Task ExistingDriverOpeningBlocksDuplicateFirstDayOpeningThroughTheJournalEndpoint()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await SetUpAsync(db);
        Assert.True((await CreateService(db, 1).AddAsync(Request())).IsSuccess);
        var company = new TestCurrentCompanyContext(1);
        var journal = new JournalEntryService(db.Context, company, TimeProvider.System,
            new FiscalYearQueryScopeResolver(db.Context, company));

        var created = await journal.AddAsync(new JournalEntryRequest(
            FiscalYearId: 1, EntryDate: new(2026, 1, 1), Description: "Duplicate",
            EntryType: JournalEntryType.Opening,
            Lines: [
                new JournalEntryLineRequest(
                    AccountId: 101, Description: null, Debit: 30m, Credit: 0m,
                    PartyType: JournalPartyType.Driver, PartyId: 1),
                new JournalEntryLineRequest(AccountId: 102, Description: null, Debit: 0m, Credit: 30m)
            ]));

        Assert.Equal("JournalEntries.DriverOpeningBalanceConflict", created.Error.Code);
    }

    [Fact]
    public async Task PostedLegacyDriverOpeningPreventsDuplicateWithoutBecomingADocument()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await SetUpAsync(db);
        await db.ConfigureSeparateFiscalYearsAsync();
        await SeedOpeningJournalAsync(db, sourceType: null);
        var service = CreateService(db, 1);

        Assert.Equal("DriverOpeningBalances.LegacyOpeningExists",
            (await service.AddAsync(Request() with { FiscalYearId = 100 })).Error.Code);
        Assert.Empty((await service.GetAllAsync(Page(), 100, 1)).Value.Items);
    }

    private static DriverOpeningBalanceService CreateService(CashManagementTestDatabase db, int companyId)
    {
        var company = new TestCurrentCompanyContext(companyId);
        return new DriverOpeningBalanceService(
            db.Context,
            company,
            new FiscalYearQueryScopeResolver(db.Context, company),
            new OpeningBalancePostingService(
                db.Context,
                company,
                new AccountMappingResolver(db.Context, company),
                new AutomaticPostingService(db.Context, company, TimeProvider.System,
                    NullLogger<AutomaticPostingService>.Instance)));
    }

    private static PaginationRequest Page() => new() { PageNumber = 1, PageSize = 20 };

    private sealed record TestCurrentCompanyContext(int CompanyId) : ICurrentCompanyContext;

    private static DriverOpeningBalanceRequest Request(
        DriverOpeningBalanceType type = DriverOpeningBalanceType.Receivable) =>
        new(DriverId: 1, FiscalYearId: 1, Amount: 123.45m, BalanceType: type, Notes: " Opening ");

    private static Task SeedOpeningJournalAsync(CashManagementTestDatabase db, int? sourceType) =>
        db.Context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO JournalEntries (
                Id, CompanyId, FiscalYearId, EntryNumber, EntryDate, Description, EntryType,
                SourceType, SourceId, SourceNumber, Status, PostedOn, RowVersion,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (801, 1, 100, 'OB-2025', '2026-01-01', 'Opening', 3,
                {sourceType}, CASE WHEN {sourceType} IS NULL THEN NULL ELSE 1 END,
                '2025', 1, '2026-01-01', randomblob(8), 'test', '2026-01-01', 'test', 0);

            INSERT INTO JournalEntryLines (
                CompanyId, JournalEntryId, AccountId, PartyType, PartyId, Debit, Credit, Currency,
                ExchangeRate, TransactionDebit, TransactionCredit, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (1, 801, 101, 4, 1, 30, 0, 1, 1, 30, 0, 'test', '2026-01-01', 'test', 0),
                   (1, 801, 101, 4, 1, 0, 10, 1, 1, 0, 10, 'test', '2026-01-01', 'test', 0),
                   (1, 801, 102, NULL, NULL, 0, 20, 1, 1, 0, 20, 'test', '2026-01-01', 'test', 0);
            """);

    private static Task SetUpAsync(CashManagementTestDatabase db) =>
        db.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Accounts (
                Id, CompanyId, Code, Name, ParentAccountId, AccountType, NormalBalance, IsPosting, IsActive, IsDeleted)
            VALUES (101, 1, '2300', 'Driver Control', 50, 2, 2, 1, 1, 0),
                   (102, 1, '3100', 'Opening Equity', 50, 3, 2, 1, 1, 0);
            INSERT INTO AccountMappings (CompanyId, FiscalYearId, MappingType, AccountId,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (1, 1, 12, 101, 'test', '2026-01-01', 'test', 0),
                   (1, 1, 17, 102, 'test', '2026-01-01', 'test', 0);
            """);
}
