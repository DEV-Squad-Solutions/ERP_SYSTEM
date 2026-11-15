using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Cashboxes;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Services.AccountMappings;
using MiniErp.Infrastructure.Services.Cashboxes;
using MiniErp.Infrastructure.Services.JournalEntries;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.CashManagement;

public sealed class CashboxDeactivationTests
{
    static CashboxDeactivationTests() =>
        MappingConfiguration.Register(typeof(InfrastructureAssemblyMarker).Assembly);

    [Theory]
    [InlineData(CashDirection.Receipt, 0.01)]
    [InlineData(CashDirection.Payment, 0.01)]
    public async Task NonZeroNativeBalance_RejectsDeactivationWithoutChangingCashboxOrJournals(
        CashDirection direction, decimal amount)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SetOpeningAsync(database, openingBalance: 0m);
        await SeedVoucherAsync(database, id: 8001, direction: direction, amount: amount);
        var service = database.CreateCashboxService(companyId: 1);
        var original = (await service.GetByIdAsync(1)).Value;
        var journalCount = await database.Context.JournalEntries.CountAsync();

        var result = await service.UpdateAsync(1, Update(original, isActive: false) with { Name = "Should not persist" });

        Assert.True(result.IsFailure);
        Assert.Equal("Cashboxes.NonZeroBalance", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        database.Context.ChangeTracker.Clear();
        var retained = (await service.GetByIdAsync(1)).Value;
        Assert.True(retained.IsActive);
        Assert.Equal(original.Name, retained.Name);
        Assert.Equal(original.RowVersion, retained.RowVersion);
        Assert.Equal(direction == CashDirection.Receipt ? 0.01m : -0.01m, retained.CurrentBalance);
        Assert.Equal(journalCount, await database.Context.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task PostedHistoryThatNetsToZero_DeactivatesWithoutRepostingAndRemainsReadable()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await ConfigureOpeningPostingAsync(database);
        await SeedVoucherAsync(database, id: 8001, direction: CashDirection.Receipt, amount: 125m);
        await SeedVoucherAsync(database, id: 8002, direction: CashDirection.Payment, amount: 1125m);
        var service = CreateServiceWithRealOpeningPosting(database);
        var original = (await service.GetByIdAsync(1)).Value;
        var journalVersion = await database.Context.JournalEntries.AsNoTracking()
            .Where(entry => entry.Id == 1001).Select(entry => entry.RowVersion).SingleAsync();
        Assert.Equal(0m, original.CurrentBalance);

        var result = await service.UpdateAsync(1, Update(original, isActive: false));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.False(result.Value.IsActive);
        Assert.Equal(0m, result.Value.CurrentBalance);
        Assert.Equal(1000m, result.Value.OpeningBalance);
        Assert.Equal(original.BaseOpeningBalance, result.Value.BaseOpeningBalance);
        Assert.Equal(journalVersion, await database.Context.JournalEntries.AsNoTracking()
            .Where(entry => entry.Id == 1001).Select(entry => entry.RowVersion).SingleAsync());
        Assert.Equal(2, await database.Context.CashVouchers.CountAsync());
        Assert.DoesNotContain((await service.GetSelectAsync()).Value, row => row.Id == 1);
        Assert.False((await service.GetByIdAsync(1)).Value.IsActive);
        var list = await service.GetAllAsync(new PaginationRequest(), new CashboxFilterRequest(IsActive: false));
        Assert.Contains(list.Value.Items, row => row.Id == 1 && row.CurrentBalance == 0m);
    }

    [Fact]
    public async Task MetadataAndStatusOnly_WithClosedOpeningYear_DoesNotReplayOpeningJournal()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await SetOpeningAsync(database, openingBalance: 1000m, openingDate: "2025-01-01");
        var service = CreateServiceWithRealOpeningPosting(database);
        var original = (await service.GetByIdAsync(1)).Value;
        Assert.Equal(0m, original.CurrentBalance);
        var journalCount = await database.Context.JournalEntries.CountAsync();

        var result = await service.UpdateAsync(1, Update(original, isActive: false) with
        {
            Name = "Historical cashbox", Notes = "Closed opening year"
        });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.False(result.Value.IsActive);
        Assert.Equal("Historical cashbox", result.Value.Name);
        Assert.Equal("Closed opening year", result.Value.Notes);
        Assert.Equal(1000m, result.Value.OpeningBalance);
        Assert.Equal(journalCount, await database.Context.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task CurrentNativeCarryAndPostedVouchers_DetermineZeroIgnoringOtherYearsAndExcludedRows()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE Cashboxes SET Currency = 3, OpeningBalanceDate = '2025-01-01' WHERE Id = 1;");
        await SeedCarryAsync(database, id: 8001, nativeDebit: 100m, nativeCredit: 0m,
            baseDebit: 5000m, currency: CurrencyCode.EUR);
        await SeedCarryAsync(database, id: 8002, nativeDebit: 90m, nativeCredit: 0m,
            baseDebit: 90m, currency: CurrencyCode.EGP);
        await SeedCarryAsync(database, id: 8003, nativeDebit: 77m, nativeCredit: 0m,
            baseDebit: 3850m, currency: CurrencyCode.EUR, isDeleted: true);
        await SeedCarryAsync(database, id: 8004, nativeDebit: 66m, nativeCredit: 0m,
            baseDebit: 3300m, currency: CurrencyCode.EUR, status: JournalEntryStatus.Reversed);
        await SeedVoucherAsync(database, id: 8101, direction: CashDirection.Payment, amount: 100m, fiscalYearId: 100);
        await SeedVoucherAsync(database, id: 8102, direction: CashDirection.Receipt, amount: 777m, fiscalYearId: 1);
        await SeedVoucherAsync(database, id: 8103, direction: CashDirection.Receipt, amount: 888m, fiscalYearId: 100, isPosted: false);
        await SeedVoucherAsync(database, id: 8104, direction: CashDirection.Receipt, amount: 999m, fiscalYearId: 100, isDeleted: true);
        await SeedVoucherAsync(database, id: 8105, direction: CashDirection.Receipt, amount: 555m, fiscalYearId: 100, companyId: 2);
        var service = database.CreateCashboxService(companyId: 1);
        var original = (await service.GetByIdAsync(1)).Value;

        var result = await service.UpdateAsync(1, Update(original, isActive: false));

        Assert.Equal(0m, original.CurrentBalance);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.Equal(CurrencyCode.EUR, result.Value.Currency);
        Assert.Equal(0m, result.Value.CurrentBalance);
        Assert.False(result.Value.IsActive);
    }

    [Theory]
    [InlineData(25, 0)]
    [InlineData(0, 25)]
    public async Task CurrentNativeCarryWithNonZeroBalance_RejectsDeactivation(decimal debit, decimal credit)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await SetOpeningAsync(database, openingBalance: 1000m, openingDate: "2025-01-01");
        await SeedCarryAsync(database, id: 8001, nativeDebit: debit, nativeCredit: credit,
            baseDebit: debit, currency: CurrencyCode.EGP);
        var service = database.CreateCashboxService(companyId: 1);
        var original = (await service.GetByIdAsync(1)).Value;

        var result = await service.UpdateAsync(1, Update(original, isActive: false));

        Assert.Equal(debit - credit, original.CurrentBalance);
        Assert.Equal("Cashboxes.NonZeroBalance", result.Error.Code);
        Assert.True((await service.GetByIdAsync(1)).Value.IsActive);
    }

    [Theory]
    [InlineData(15, 0)]
    [InlineData(0, 15)]
    public async Task PostedManualNativeCash_DeterminesDisplayedBalanceAndBlocksDeactivation(decimal debit, decimal credit)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SetOpeningAsync(database, openingBalance: 0m);
        await SeedManualAsync(database, id: 8001, nativeDebit: debit, nativeCredit: credit);
        var service = database.CreateCashboxService(companyId: 1);
        var original = (await service.GetByIdAsync(1)).Value;

        var result = await service.UpdateAsync(1, Update(original, isActive: false));

        Assert.Equal(debit - credit, original.CurrentBalance);
        Assert.Equal("Cashboxes.NonZeroBalance", result.Error.Code);
        Assert.Equal(debit - credit, Assert.Single((await service.GetSelectAsync()).Value, row => row.Id == 1).CurrentBalance);
        Assert.Equal(debit - credit, Assert.Single((await service.GetAllAsync(new PaginationRequest())).Value.Items, row => row.Id == 1).CurrentBalance);
    }

    [Fact]
    public async Task ManualNativePaymentCanZeroOpening_ExcludedManualRowsDoNotChangeCash()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SeedManualAsync(database, id: 8001, nativeDebit: 0m, nativeCredit: 1000m);
        await SeedManualAsync(database, id: 8002, nativeDebit: 700m, nativeCredit: 0m, status: JournalEntryStatus.Reversed);
        await SeedManualAsync(database, id: 8003, nativeDebit: 800m, nativeCredit: 0m, isDeleted: true);
        await SeedManualAsync(database, id: 8004, nativeDebit: 900m, nativeCredit: 0m, currency: CurrencyCode.EUR);
        await SeedManualAsync(database, id: 8005, nativeDebit: 500m, nativeCredit: 0m, companyId: 2);
        await SeedManualAsync(database, id: 8006, nativeDebit: 600m, nativeCredit: 0m, reversalOfEntryId: 8001);
        await SeedManualAsync(database, id: 8007, nativeDebit: 400m, nativeCredit: 0m);
        await database.Context.Database.ExecuteSqlRawAsync("UPDATE JournalEntries SET ReversedOn = '2026-08-02' WHERE Id = 8007;");
        var service = database.CreateCashboxService(companyId: 1);
        var original = (await service.GetByIdAsync(1)).Value;

        var result = await service.UpdateAsync(1, Update(original, isActive: false));

        Assert.Equal(0m, original.CurrentBalance);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.False(result.Value.IsActive);
    }

    [Fact]
    public async Task MissingCurrentFiscalYear_DoesNotTreatDisplayDefaultZeroAsPermissionToDeactivate()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SetOpeningAsync(database, openingBalance: 0m);
        await database.Context.Database.ExecuteSqlRawAsync("UPDATE FiscalYears SET IsCurrent = 0 WHERE CompanyId = 1;");
        var service = database.CreateCashboxService(companyId: 1);
        var original = (await service.GetByIdAsync(1)).Value;
        Assert.Equal(0m, original.CurrentBalance);

        var result = await service.UpdateAsync(1, Update(original, isActive: false));

        Assert.Equal("FiscalYears.CurrentNotFound", result.Error.Code);
        Assert.True((await service.GetByIdAsync(1)).Value.IsActive);
    }

    [Fact]
    public async Task LatestTrackedStateAndRevision_AreReloadedBeforeDeactivationGuard()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var tracked = await database.Context.Cashboxes.SingleAsync(cashbox => cashbox.Id == 3);
        Assert.False(tracked.IsActive);
        await database.Context.Database.ExecuteSqlRawAsync("UPDATE Cashboxes SET IsActive = 1 WHERE Id = 3;");
        var service = database.CreateCashboxService(companyId: 1);
        var current = (await service.GetByIdAsync(3)).Value;

        var result = await service.UpdateAsync(3, Update(current, isActive: false));

        Assert.Equal("Cashboxes.NonZeroBalance", result.Error.Code);
        database.Context.ChangeTracker.Clear();
        Assert.True((await service.GetByIdAsync(3)).Value.IsActive);
    }

    [Fact]
    public async Task StaleRevision_ReturnsConcurrencyBeforeBalanceValidation()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.Context.Cashboxes.SingleAsync(cashbox => cashbox.Id == 1);
        var service = database.CreateCashboxService(companyId: 1);
        var original = (await service.GetByIdAsync(1)).Value;
        await database.Context.Database.ExecuteSqlRawAsync("UPDATE Cashboxes SET Name = 'Concurrent edit' WHERE Id = 1;");

        var result = await service.UpdateAsync(1, Update(original, isActive: false));

        Assert.Equal("Cashboxes.Concurrency", result.Error.Code);
        database.Context.ChangeTracker.Clear();
        Assert.Equal("Concurrent edit", (await service.GetByIdAsync(1)).Value.Name);
    }

    [Fact]
    public async Task InactiveMetadataEditsAndReactivation_AreAllowedWithExistingNonZeroBalance()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = database.CreateCashboxService(companyId: 1);
        var original = (await service.GetByIdAsync(3)).Value;

        var metadata = await service.UpdateAsync(3, Update(original, isActive: false) with { Notes = "Historical note" });
        Assert.True(metadata.IsSuccess, metadata.IsFailure ? metadata.Error.Code : null);
        Assert.False(metadata.Value.IsActive);
        Assert.Equal(500m, metadata.Value.CurrentBalance);
        var reactivated = await service.UpdateAsync(3, Update(metadata.Value, isActive: true));

        Assert.True(reactivated.IsSuccess);
        Assert.True(reactivated.Value.IsActive);
        Assert.Contains((await service.GetSelectAsync()).Value, row => row.Id == 3);
    }

    [Fact]
    public async Task Deactivation_RequiresExecuteAndTenantOwnershipWithoutChangingState()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var original = (await database.CreateCashboxService(companyId: 1).GetByIdAsync(1)).Value;
        var restricted = database.CreateCashboxService(companyId: 1,
            cashboxAccessService: new RestrictedCashboxAccess(view: [1], execute: []));

        var denied = await restricted.UpdateAsync(1, Update(original, isActive: false));
        var wrongCompany = await database.CreateCashboxService(companyId: 2)
            .UpdateAsync(1, Update(original, isActive: false));

        Assert.Equal(ErrorType.Forbidden, denied.Error.Type);
        Assert.Equal("Cashboxes.NotFound", wrongCompany.Error.Code);
        Assert.True((await database.CreateCashboxService(companyId: 1).GetByIdAsync(1)).Value.IsActive);
    }

    [Fact]
    public async Task CombinedOpeningChange_CannotCreateNonZeroInactiveCashbox()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SetOpeningAsync(database, openingBalance: 0m);
        var service = database.CreateCashboxService(companyId: 1);
        var original = (await service.GetByIdAsync(1)).Value;

        var result = await service.UpdateAsync(1, Update(original, isActive: false) with { OpeningBalance = 100m });

        Assert.Equal("Cashboxes.NonZeroBalance", result.Error.Code);
        database.Context.ChangeTracker.Clear();
        var retained = (await service.GetByIdAsync(1)).Value;
        Assert.True(retained.IsActive);
        Assert.Equal(0m, retained.OpeningBalance);
        Assert.Equal(original.RowVersion, retained.RowVersion);
    }

    [Fact]
    public async Task FinancialOpeningChange_StillSynchronizesOpeningJournal()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await ConfigureOpeningPostingAsync(database);
        var service = CreateServiceWithRealOpeningPosting(database);
        var original = (await service.GetByIdAsync(1)).Value;

        var result = await service.UpdateAsync(1, Update(original, isActive: true) with { OpeningBalance = 1500m });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.Equal(1500m, result.Value.OpeningBalance);
        Assert.Equal(1500m, result.Value.BaseOpeningBalance);
        var opening = await database.Context.JournalEntryLines.AsNoTracking()
            .Where(line => line.JournalEntry.SourceType == JournalEntrySourceType.CashboxOpeningBalance &&
                line.JournalEntry.SourceId == 1 && line.PartyType == JournalPartyType.Cashbox &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null && line.JournalEntry.ReversedOn == null)
            .SingleAsync();
        Assert.Equal(1500m, opening.Debit);
        Assert.Equal(0m, opening.Credit);
        Assert.Equal(1500m, opening.TransactionDebit);
        Assert.Equal(0m, opening.TransactionCredit);
    }

    [Fact]
    public async Task DeletedVoucherHistory_DoesNotPreventSoftDeletingCashbox()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SeedVoucherAsync(database, id: 8001, direction: CashDirection.Receipt, amount: 50m, isDeleted: true);
        await database.Context.CashVouchers.IgnoreQueryFilters().Include(row => row.Cashbox)
            .SingleAsync(row => row.Id == 8001);
        var service = database.CreateCashboxService(companyId: 1);

        var result = await service.DeleteAsync(1);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        database.Context.ChangeTracker.Clear();
        var deleted = await database.Context.Cashboxes.IgnoreQueryFilters().SingleAsync(row => row.Id == 1);
        Assert.True(deleted.IsDeleted);
        Assert.False(deleted.IsActive);
        Assert.NotNull(deleted.DeletedOn);
        Assert.Equal("Cashboxes.NotFound", (await service.GetByIdAsync(1)).Error.Code);
        Assert.DoesNotContain((await service.GetSelectAsync()).Value, row => row.Id == 1);
        Assert.True(await database.Context.CashVouchers.IgnoreQueryFilters().AnyAsync(row => row.Id == 8001 && row.IsDeleted));
    }

    [Fact]
    public async Task DeleteAfterVoucherRemoval_AlsoRemovesOwnOpeningJournalAtomically()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await ConfigureOpeningPostingAsync(database);
        await SeedVoucherAsync(database, id: 8001, direction: CashDirection.Receipt, amount: 50m, isDeleted: true);
        var service = CreateServiceWithRealOpeningPosting(database);

        var result = await service.DeleteAsync(1);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        Assert.True(await database.Context.Cashboxes.IgnoreQueryFilters().AnyAsync(row => row.Id == 1 && row.IsDeleted));
        Assert.False(await database.Context.JournalEntries.AnyAsync(row => row.Id == 1001));
        Assert.True(await database.Context.JournalEntries.IgnoreQueryFilters().AnyAsync(row => row.Id == 1001 && row.IsDeleted));
    }

    [Fact]
    public async Task DeleteWithClosedOpening_RollsBackCashboxAndKeepsAccountingHistory()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await ConfigureOpeningPostingAsync(database);
        await database.Context.Database.ExecuteSqlRawAsync("UPDATE FiscalYears SET Status = 2 WHERE Id = 1 AND CompanyId = 1;");
        var service = CreateServiceWithRealOpeningPosting(database);

        var result = await service.DeleteAsync(1);

        Assert.True(result.IsFailure);
        database.Context.ChangeTracker.Clear();
        Assert.False((await database.Context.Cashboxes.SingleAsync(row => row.Id == 1)).IsDeleted);
        Assert.True(await database.Context.JournalEntries.AnyAsync(row => row.Id == 1001));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARemainingVoucherInAnyYear_PreventsCashboxDeletion(bool posted)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigureSeparateFiscalYearsAsync();
        await SeedVoucherAsync(database, id: 8001, direction: CashDirection.Receipt, amount: 50m,
            fiscalYearId: 1, isPosted: posted);
        await SeedVoucherAsync(database, id: 8002, direction: CashDirection.Payment, amount: 50m,
            fiscalYearId: 100, isDeleted: true);
        var service = database.CreateCashboxService(companyId: 1);

        var result = await service.DeleteAsync(1);

        Assert.Equal("Cashboxes.HasVouchers", result.Error.Code);
        Assert.False((await database.Context.Cashboxes.SingleAsync(row => row.Id == 1)).IsDeleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualJournalIsADeletionDependencyOnlyWhileItIsActive(bool deleted)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SeedVoucherAsync(database, id: 8001, direction: CashDirection.Receipt, amount: 50m, isDeleted: true);
        await SeedManualAsync(database, id: 8002, nativeDebit: 25m, nativeCredit: 0m, isDeleted: deleted);
        var service = database.CreateCashboxService(companyId: 1);

        var result = await service.DeleteAsync(1);

        Assert.Equal(deleted, result.IsSuccess);
        if (!deleted) Assert.Equal("Cashboxes.HasMovements", result.Error.Code);
    }

    [Fact]
    public async Task ActiveTransferWithoutVoucherRows_StillPreventsCashboxDeletion()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        database.Context.CashboxTransfers.Add(new MiniErp.Domain.Entities.CashManagement.CashboxTransfer
        {
            CompanyId = 1, FiscalYearId = 1, SourceCashboxId = 1, DestinationCashboxId = 2,
            TransferNumber = "TRANSFER-DEPENDENCY", TransferDate = new DateOnly(2026, 8, 1)
        });
        await database.Context.SaveChangesAsync();
        var result = await database.CreateCashboxService(companyId: 1).DeleteAsync(1);
        Assert.Equal("Cashboxes.HasMovements", result.Error.Code);
    }

    private static CashboxUpdateRequest Update(CashboxResponse cashbox, bool isActive) => new(
        Name: cashbox.Name, Currency: cashbox.Currency, OpeningBalance: cashbox.OpeningBalance,
        IsActive: isActive, Notes: cashbox.Notes, RowVersion: cashbox.RowVersion,
        OpeningBalanceDate: cashbox.OpeningBalanceDate, OpeningExchangeRate: cashbox.OpeningExchangeRate);

    private static Task SetOpeningAsync(CashManagementTestDatabase database, decimal openingBalance,
        string openingDate = "2026-01-01") => database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Cashboxes SET OpeningBalance = {openingBalance}, BaseOpeningBalance = {openingBalance}, OpeningBalanceDate = {openingDate} WHERE Id = 1;");

    private static Task SeedVoucherAsync(CashManagementTestDatabase database, int id,
        CashDirection direction, decimal amount, int fiscalYearId = 1,
        bool isPosted = true, bool isDeleted = false, int companyId = 1) =>
        database.Context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO CashVouchers (Id, CompanyId, FiscalYearId, VoucherNumber, VoucherDate,
                Direction, CashboxId, PartyType, Amount, Currency, ExchangeRate, BaseAmount,
                IsPosted, LastModifiedAt, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES ({id}, {companyId}, {fiscalYearId}, {"CV-" + id}, '2026-08-01',
                {(int)direction}, 1, 1, {amount}, 1, 1, {amount}, {(isPosted ? 1 : 0)},
                '2026-08-01', 'test', '2026-08-01', 'test', {(isDeleted ? 1 : 0)});
            """);

    private static Task SeedCarryAsync(CashManagementTestDatabase database, int id,
        decimal nativeDebit, decimal nativeCredit, decimal baseDebit, CurrencyCode currency,
        bool isDeleted = false, JournalEntryStatus status = JournalEntryStatus.Posted) =>
        database.SeedPostedJournalEntryAsync(journalEntryId: id, entryNumber: "CARRY-" + id,
            entryDate: new DateOnly(2026, 1, 1), entryType: JournalEntryType.Opening,
            sourceType: JournalEntrySourceType.FiscalYearClosing, sourceId: id, sourceNumber: "CARRY-" + id,
            lines: [new CashManagementTestDatabase.JournalEntryLineSeed(
                AccountId: 100, PartyType: JournalPartyType.Cashbox, PartyId: 1,
                Debit: baseDebit, Credit: 0m, Currency: currency, ExchangeRate: 50m,
                TransactionDebit: nativeDebit, TransactionCredit: nativeCredit)],
            status: status, isDeleted: isDeleted, fiscalYearId: 100);

    private static Task SeedManualAsync(CashManagementTestDatabase database, int id,
        decimal nativeDebit, decimal nativeCredit, CurrencyCode currency = CurrencyCode.EGP,
        JournalEntryStatus status = JournalEntryStatus.Posted, bool isDeleted = false,
        int companyId = 1, int? reversalOfEntryId = null) =>
        database.SeedPostedJournalEntryAsync(journalEntryId: id, entryNumber: "MANUAL-" + id,
            entryDate: new DateOnly(2026, 8, 1), entryType: JournalEntryType.Manual,
            sourceType: null, sourceId: null, sourceNumber: null,
            lines: [new CashManagementTestDatabase.JournalEntryLineSeed(
                AccountId: 100, PartyType: JournalPartyType.Cashbox, PartyId: 1,
                Debit: nativeDebit, Credit: nativeCredit, Currency: currency, ExchangeRate: 1m,
                TransactionDebit: nativeDebit, TransactionCredit: nativeCredit)],
            status: status, isDeleted: isDeleted, companyId: companyId, reversalOfEntryId: reversalOfEntryId);

    private static Task ConfigureOpeningPostingAsync(CashManagementTestDatabase database) =>
        database.Context.Database.ExecuteSqlRawAsync("""
            INSERT INTO Accounts (Id, CompanyId, Code, Name, ParentAccountId, AccountType,
                NormalBalance, IsPosting, IsActive, IsDeleted)
            VALUES (102, 1, '3100', 'Opening Equity', 50, 3, 2, 1, 1, 0);
            INSERT INTO AccountMappings (CompanyId, FiscalYearId, MappingType, AccountId,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (1, 1, 17, 102, 'test', '2026-01-01', 'test', 0);
            UPDATE JournalEntries SET EntryType = 4
            WHERE CompanyId = 1 AND SourceType = 11 AND SourceId = 1;
            """);

    private static CashboxService CreateServiceWithRealOpeningPosting(CashManagementTestDatabase database)
    {
        var company = new TestCompanyContext(CompanyId: 1);
        return new CashboxService(database.Context, new PaginationService(), company,
            new TestExchangeRateResolver(), TimeProvider.System,
            new OpeningBalancePostingService(database.Context, company,
                new AccountMappingResolver(database.Context, company),
                new AutomaticPostingService(database.Context, company, TimeProvider.System,
                    NullLogger<AutomaticPostingService>.Instance)));
    }

    private sealed record TestCompanyContext(int CompanyId) : ICurrentCompanyContext;
}
