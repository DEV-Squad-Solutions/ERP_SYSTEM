using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.Statements;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.ExchangeRates;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Infrastructure.Services.AccountMappings;
using MiniErp.Infrastructure.Services.CashVouchers;
using MiniErp.Infrastructure.Services.FiscalYears;
using MiniErp.Infrastructure.Services.JournalEntries;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Services.ExchangeRates;

namespace MiniErp.Tests.CashManagement;

public sealed class CrossCurrencyPartnerVoucherTests
{
    static CrossCurrencyPartnerVoucherTests() =>
        MappingConfiguration.Register(typeof(InfrastructureAssemblyMarker).Assembly);

    [Theory]
    [InlineData(CashDirection.Payment, 8, JournalPartyType.Customer)]
    [InlineData(CashDirection.Receipt, 7, JournalPartyType.Supplier)]
    public async Task DollarCashSettlesEgyptianPartnerWithCorrectRole(
        CashDirection direction, int movementTypeId, JournalPartyType role)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigurePaymentPostingAsync();
        var service = database.CreatePostingVoucherService(1);
        var draft = await service.AddAsync(Draft(direction: direction, amount: 500m, cashboxId: 5, rate: 50m));
        Assert.True(draft.IsSuccess, draft.Error.Description);
        var result = await service.UpdateAsync(draft.Value.Id,
            Complete(draft.Value, partnerId: 1, movementTypeId: movementTypeId));
        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(CurrencyCode.USD, result.Value.Currency);
        Assert.Equal(500m, result.Value.Amount);
        Assert.Equal(50m, result.Value.ExchangeRate);
        Assert.Equal(25_000m, result.Value.BaseAmount);
        Assert.Equal(CurrencyCode.EGP, result.Value.PartnerCurrency);
        Assert.Equal(25_000m, result.Value.PartnerAmount);
        Assert.Equal(1m, result.Value.PartnerExchangeRate);
        Assert.Equal(25_000m, result.Value.PartnerBaseAmount);
        var movement = await database.Context.BusinessPartnerMovements.SingleAsync(m => m.CashVoucherId == result.Value.Id);
        Assert.Equal(CurrencyCode.EGP, movement.Currency);
        Assert.Equal(direction == CashDirection.Payment ? 25_000m : 0m, movement.Debit);
        Assert.Equal(direction == CashDirection.Receipt ? 25_000m : 0m, movement.Credit);
        var lines = await database.Context.JournalEntryLines
            .Where(l => l.JournalEntry.SourceType == JournalEntrySourceType.CashVoucher && l.JournalEntry.SourceId == result.Value.Id)
            .ToListAsync();
        var cashLine = Assert.Single(lines, l => l.PartyType == JournalPartyType.Cashbox);
        var partnerLine = Assert.Single(lines, l => l.PartyType == role);
        Assert.Equal(CurrencyCode.USD, cashLine.Currency);
        Assert.Equal(500m, cashLine.TransactionDebit + cashLine.TransactionCredit);
        Assert.Equal(CurrencyCode.EGP, partnerLine.Currency);
        Assert.Equal(25_000m, partnerLine.TransactionDebit + partnerLine.TransactionCredit);
        Assert.Equal(role == JournalPartyType.Customer ? 110 : 111, partnerLine.AccountId);
        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
        Assert.Equal(2, lines.Count);
        var statement = await database.CreateStatementService(1).GetPartnerStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new PartnerStatementFilterRequest(BusinessPartnerId: 1, FiscalYearId: 1));
        Assert.True(statement.IsSuccess, statement.Error.Description);
        Assert.Equal(CurrencyCode.EGP, statement.Value.Currency);
        var statementRow = Assert.Single(statement.Value.Items,
            row => row.DocumentNumber == result.Value.VoucherNumber);
        Assert.Equal(direction == CashDirection.Payment ? 25_000m : 0m, statementRow.DebitAmount);
        Assert.Equal(direction == CashDirection.Receipt ? 25_000m : 0m, statementRow.CreditAmount);
        Assert.Equal(1m, statementRow.ExchangeRate);
    }

    [Theory]
    [InlineData(CashDirection.Payment)]
    [InlineData(CashDirection.Receipt)]
    public async Task EgyptianCashSettlesDollarPartner(CashDirection direction)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigurePaymentPostingAsync();
        var service = database.CreatePostingVoucherService(1);
        var draft = await service.AddAsync(Draft(direction, amount: 25_000m, cashboxId: 1, rate: 1m));
        var result = await service.UpdateAsync(draft.Value.Id,
            Complete(draft.Value, partnerId: 5) with { PartnerExchangeRate = 50m });
        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(CurrencyCode.EGP, result.Value.Currency);
        Assert.Equal(25_000m, result.Value.Amount);
        Assert.Equal(CurrencyCode.USD, result.Value.PartnerCurrency);
        Assert.Equal(500m, result.Value.PartnerAmount);
        Assert.Equal(50m, result.Value.PartnerExchangeRate);
        var lines = await database.Context.JournalEntryLines
            .Where(l => l.JournalEntry.SourceType == JournalEntrySourceType.CashVoucher && l.JournalEntry.SourceId == result.Value.Id)
            .ToListAsync();
        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
        var partnerLine = Assert.Single(lines, l => l.PartyType is JournalPartyType.Customer or JournalPartyType.Supplier);
        Assert.Equal(CurrencyCode.USD, partnerLine.Currency);
        Assert.Equal(500m, partnerLine.TransactionDebit + partnerLine.TransactionCredit);
    }

    [Theory]
    [InlineData(CashDirection.Payment)]
    [InlineData(CashDirection.Receipt)]
    public async Task TwoForeignCurrenciesPreserveNativeAmountsAndBalanceRounding(CashDirection direction)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigurePaymentPostingAsync();
        await database.Context.Database.ExecuteSqlRawAsync("UPDATE Cashboxes SET Currency = 3 WHERE Id = 5;");
        var service = database.CreatePostingVoucherService(1);
        const decimal cashRate = 50.123456789m;
        const decimal partnerRate = 53.123456789m;
        var draft = await service.AddAsync(Draft(direction, amount: 123.45m, cashboxId: 5, rate: cashRate));
        var result = await service.UpdateAsync(draft.Value.Id,
            Complete(draft.Value, partnerId: 5) with { PartnerExchangeRate = partnerRate });
        Assert.True(result.IsSuccess, result.Error.Description);
        var expectedNative = ExchangeRateRules.ConvertFromBase(
            ExchangeRateRules.ConvertToBase(123.45m, cashRate), partnerRate);
        Assert.Equal(CurrencyCode.EUR, result.Value.Currency);
        Assert.Equal(123.45m, result.Value.Amount);
        Assert.Equal(CurrencyCode.USD, result.Value.PartnerCurrency);
        Assert.Equal(expectedNative, result.Value.PartnerAmount);
        Assert.Equal(partnerRate, result.Value.PartnerExchangeRate);
        Assert.Equal(ExchangeRateRules.ConvertToBase(expectedNative, partnerRate), result.Value.PartnerBaseAmount);
        var lines = await database.Context.JournalEntryLines
            .Where(l => l.JournalEntry.SourceType == JournalEntrySourceType.CashVoucher && l.JournalEntry.SourceId == result.Value.Id)
            .ToListAsync();
        Assert.Equal(3, lines.Count);
        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
        var counterpart = Assert.Single(lines, l => l.PartyType is JournalPartyType.Customer or JournalPartyType.Supplier);
        Assert.Equal(expectedNative, counterpart.TransactionDebit + counterpart.TransactionCredit);
        var difference = Assert.Single(lines, l => l.PartyType == null);
        var expectedDifference = direction == CashDirection.Receipt
            ? result.Value.BaseAmount - result.Value.PartnerBaseAmount!.Value
            : result.Value.PartnerBaseAmount!.Value - result.Value.BaseAmount;
        Assert.Equal(expectedDifference < 0m, difference.Debit > 0m);
        Assert.Equal(expectedDifference > 0m, difference.Credit > 0m);
    }

    [Fact]
    public async Task MissingPartnerRateFailsAtomicallyAndChangingTargetClearsSnapshot()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigurePaymentPostingAsync();
        await EnsureRateSchemaAsync(database);
        var resolver = new ExchangeRateResolver(database.Context,
            new TestCompanyContext(), TimeProvider.System);
        var service = database.CreatePostingVoucherService(1, exchangeRateResolver: resolver);
        var draft = await service.AddAsync(Draft(CashDirection.Receipt, 25_000m, 1, 1m));
        var failed = await service.UpdateAsync(draft.Value.Id, Complete(draft.Value, 5));
        Assert.True(failed.IsFailure);
        Assert.Equal("ExchangeRates.Missing", failed.Error.Code);
        database.Context.ChangeTracker.Clear();
        Assert.False((await database.Context.CashVouchers.SingleAsync(v => v.Id == draft.Value.Id)).IsPosted);
        var posted = await service.UpdateAsync(draft.Value.Id, Complete(draft.Value, 5) with { PartnerExchangeRate = 50m });
        Assert.True(posted.IsSuccess, posted.Error.Description);
        // Simulate the next HTTP request's fresh scoped context. SQLite's AFTER
        // UPDATE rowversion trigger runs after EF's RETURNING value is captured.
        database.Context.ChangeTracker.Clear();
        var changed = await service.UpdateAsync(posted.Value.Id,
            Complete(posted.Value, 5) with { BusinessPartnerId = null, AccountId = 1 });
        Assert.True(changed.IsSuccess, changed.Error.Description);
        Assert.Null(changed.Value.PartnerCurrency);
        Assert.Null(changed.Value.PartnerAmount);
        Assert.Null(changed.Value.PartnerExchangeRate);
        Assert.Null(changed.Value.PartnerBaseAmount);
        Assert.Empty(await database.Context.BusinessPartnerMovements.Where(m => m.CashVoucherId == draft.Value.Id).ToListAsync());
    }

    [Fact]
    public async Task BulkAddPassesPartnerRateAndReturnsPartnerSnapshot()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigurePaymentPostingAsync();
        var service = database.CreatePostingVoucherService(1);
        var result = await service.BulkAsync(new CashVoucherBulkRequest(Items:
        [
            new CashVoucherBulkAddItemRequest(Voucher: new CashVoucherBulkVoucherRequest(
                VoucherDate: new DateOnly(2026, 7, 27), Direction: CashDirection.Receipt,
                CashboxId: 1, CashMovementTypeId: 7, EmployeeId: null,
                BusinessPartnerId: 5, DriverId: null, DriverTripId: null,
                ExternalPartyName: null, Amount: 25_000m, ReferenceNumber: null,
                Description: "Bulk foreign settlement", Notes: null,
                ExchangeRate: 1m, PartnerExchangeRate: 50m))
        ]));
        Assert.True(result.IsSuccess, result.Error.Description);
        var voucher = Assert.Single(result.Value.Items).Voucher!;
        Assert.False(voucher.IsDraft);
        Assert.Equal(CurrencyCode.EGP, voucher.Currency);
        Assert.Equal(25_000m, voucher.Amount);
        Assert.Equal(CurrencyCode.USD, voucher.PartnerCurrency);
        Assert.Equal(500m, voucher.PartnerAmount);
        Assert.Equal(50m, voucher.PartnerExchangeRate);
        Assert.Equal(25_000m, voucher.PartnerBaseAmount);
        var read = await service.GetByIdAsync(voucher.Id);
        Assert.True(read.IsSuccess, read.Error.Description);
        Assert.Equal(500m, read.Value.PartnerAmount);
        Assert.Equal(50m, read.Value.PartnerExchangeRate);
    }

    private static CashVoucherRequest Draft(CashDirection direction, decimal amount, int cashboxId, decimal rate) =>
        new(VoucherDate: new DateOnly(2026, 7, 27), Direction: direction,
            CashboxId: cashboxId, Amount: amount, Description: "Cross-currency settlement", ExchangeRate: rate);

    [Fact]
    public async Task RateCascadePreservesSettledNativeQuantityAndProtectsBothReferences()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await database.ConfigurePaymentPostingAsync();
        await EnsureRateSchemaAsync(database);
        await database.Context.Database.ExecuteSqlRawAsync("UPDATE Cashboxes SET Currency = 3 WHERE Id = 5;");
        var context = database.Context;
        var company = new TestCompanyContext();
        var resolver = new ExchangeRateResolver(context, company, TimeProvider.System);
        var mapping = new AccountMappingResolver(context, company);
        var automaticPosting = new AutomaticPostingService(context, company, TimeProvider.System,
            NullLogger<AutomaticPostingService>.Instance);
        var voucherPosting = new CashVoucherPostingService(context, company, mapping, automaticPosting);
        // This fixture contains only standalone vouchers linked to these rates;
        // unused synchronizer dependencies deliberately fail if invoked.
        var postingSynchronizer = new ExchangeRatePostingSynchronizer(context, company,
            null!, voucherPosting, null!, null!);
        var rates = new ExchangeRateService(context, new PaginationService(), company,
            TimeProvider.System, resolver, new FiscalYearQueryScopeResolver(context, company),
            new FiscalYearPeriodGuard(context, company), exchangeRatePostingSynchronizer: postingSynchronizer);
        var usdRate = await rates.AddAsync(new ExchangeRateRequest(
            Currency: CurrencyCode.USD, RateDate: new DateOnly(2026, 7, 1), Rate: 50m));
        var eurRate = await rates.AddAsync(new ExchangeRateRequest(
            Currency: CurrencyCode.EUR, RateDate: new DateOnly(2026, 7, 1), Rate: 55m));
        Assert.True(usdRate.IsSuccess, usdRate.Error.Description);
        Assert.True(eurRate.IsSuccess, eurRate.Error.Description);
        var service = database.CreatePostingVoucherService(1, exchangeRateResolver: resolver);
        var draft = await service.AddAsync(Draft(CashDirection.Payment, 100m, 5, 55m) with { ExchangeRate = null });
        var posted = await service.UpdateAsync(draft.Value.Id,
            Complete(draft.Value, 5) with { ExchangeRate = null });
        Assert.True(posted.IsSuccess, posted.Error.Description);
        Assert.Equal(110m, posted.Value.PartnerAmount);
        context.ChangeTracker.Clear();
        var notesEdit = await service.UpdateAsync(posted.Value.Id,
            Complete(posted.Value, 5) with { ExchangeRate = null, Notes = "Notes only" });
        Assert.True(notesEdit.IsSuccess, notesEdit.Error.Description);
        context.ChangeTracker.Clear();
        var stored = await context.CashVouchers.SingleAsync(v => v.Id == posted.Value.Id);
        Assert.Equal(usdRate.Value.Id, stored.PartnerExchangeRateId);
        Assert.Equal(eurRate.Value.Id, stored.ExchangeRateId);
        var deleted = await rates.DeleteAsync(usdRate.Value.Id);
        Assert.True(deleted.IsFailure);
        Assert.Equal("ExchangeRates.Referenced", deleted.Error.Code);
        var changedPartnerRate = await rates.UpdateAsync(usdRate.Value.Id,
            new ExchangeRateUpdateRequest(Currency: CurrencyCode.USD,
                RateDate: usdRate.Value.RateDate, Rate: 60m, Source: ExchangeRateSource.Manual,
                Notes: null, RowVersion: usdRate.Value.RowVersion, UpdateLinkedTransactions: true));
        Assert.True(changedPartnerRate.IsSuccess, changedPartnerRate.Error.Description);
        context.ChangeTracker.Clear();
        stored = await context.CashVouchers.SingleAsync(v => v.Id == posted.Value.Id);
        Assert.Equal(110m, stored.PartnerAmount);
        Assert.Equal(60m, stored.PartnerExchangeRate);
        Assert.Equal(6_600m, stored.PartnerBaseAmount);
        Assert.Equal(100m, stored.Amount);
        Assert.Equal(5_500m, stored.BaseAmount);
        var changedCashRate = await rates.UpdateAsync(eurRate.Value.Id,
            new ExchangeRateUpdateRequest(Currency: CurrencyCode.EUR,
                RateDate: eurRate.Value.RateDate, Rate: 56m, Source: ExchangeRateSource.Manual,
                Notes: null, RowVersion: eurRate.Value.RowVersion, UpdateLinkedTransactions: true));
        Assert.True(changedCashRate.IsSuccess, changedCashRate.Error.Description);
        context.ChangeTracker.Clear();
        stored = await context.CashVouchers.SingleAsync(v => v.Id == posted.Value.Id);
        Assert.Equal(110m, stored.PartnerAmount);
        Assert.Equal(6_600m, stored.PartnerBaseAmount);
        Assert.Equal(100m, stored.Amount);
        Assert.Equal(5_600m, stored.BaseAmount);
        var movement = await context.BusinessPartnerMovements.SingleAsync(m => m.CashVoucherId == posted.Value.Id);
        Assert.Equal(CurrencyCode.USD, movement.Currency);
        Assert.Equal(110m, movement.Debit);
        Assert.Equal(60m, movement.ExchangeRate);
        Assert.Equal(6_600m, movement.BaseDebit);
        var lines = await context.JournalEntryLines.Where(l =>
            l.JournalEntry.SourceType == JournalEntrySourceType.CashVoucher && l.JournalEntry.SourceId == posted.Value.Id).ToListAsync();
        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
        var difference = Assert.Single(lines, l => l.PartyType == null);
        Assert.Equal(1_000m, difference.Credit);
    }

    private sealed class TestCompanyContext : ICurrentCompanyContext
    {
        public int CompanyId => 1;
    }

    private static async Task EnsureRateSchemaAsync(CashManagementTestDatabase database)
    {
        await database.Context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE ExchangeRates (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                FiscalYearId INTEGER NOT NULL,
                Currency INTEGER NOT NULL,
                RateDate TEXT NOT NULL,
                Rate NUMERIC NOT NULL,
                Source INTEGER NOT NULL,
                Provider TEXT NULL,
                Notes TEXT NULL,
                LastModifiedAt TEXT NOT NULL,
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
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );
            """);

        // The cash fixture has deliberately minimal invoice tables and no
        // employee opening balances. The cascade materializes these tables even
        // when no linked records exist. Add their mapped scalar columns locally
        // without imposing unrelated invoice/payroll seeding on cash tests.
        foreach (var table in new[] { "Invoices", "InvoiceLines", "EmployeeOpeningBalances" })
        {
            await database.Context.Database.ExecuteSqlRawAsync(
                $"CREATE TABLE IF NOT EXISTS [{table}] (Id INTEGER PRIMARY KEY);");
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = database.Context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info([{table}]);";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
            }
            foreach (var property in database.Context.Model.GetEntityTypes()
                         .Where(entity => entity.GetTableName() == table)
                         .SelectMany(entity => entity.GetProperties()))
            {
                var name = property.GetColumnName();
                if (!columns.Add(name)) continue;
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                var sqliteType = type == typeof(byte[]) ? "BLOB" :
                    type == typeof(string) || type == typeof(DateOnly) || type == typeof(DateTime) || type == typeof(Guid)
                        ? "TEXT" : "NUMERIC";
                await database.Context.Database.ExecuteSqlRawAsync(
                    $"ALTER TABLE [{table}] ADD COLUMN [{name}] {sqliteType} NULL;");
            }
        }
    }

    private static CashVoucherUpdateRequest Complete(CashVoucherResponse voucher, int partnerId, int? movementTypeId = null) =>
        new(VoucherDate: voucher.VoucherDate, Direction: voucher.Direction,
            CashboxId: voucher.CashboxId, CashMovementTypeId: movementTypeId,
            EmployeeId: null, BusinessPartnerId: partnerId, DriverId: null,
            DriverTripId: null, ExternalPartyName: null, Amount: voucher.Amount,
            ReferenceNumber: null, Description: voucher.Description, Notes: null,
            RowVersion: voucher.RowVersion, ExchangeRate: voucher.ExchangeRate);
}
