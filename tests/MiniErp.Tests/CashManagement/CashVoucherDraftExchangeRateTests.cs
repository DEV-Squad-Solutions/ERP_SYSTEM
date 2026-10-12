using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Services.CashVouchers;
using MiniErp.Infrastructure.Services.ExchangeRates;

namespace MiniErp.Tests.CashManagement;

public sealed class CashVoucherDraftExchangeRateTests
{
    static CashVoucherDraftExchangeRateTests() =>
        MappingConfiguration.Register(typeof(InfrastructureAssemblyMarker).Assembly);

    [Theory]
    [InlineData(null)]
    [InlineData("50")]
    public async Task PatchDraftRateChangesOnlySnapshotAndCompletionKeepsIt(string? initialRate)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SeedRatesAsync(database);
        var service = CreateService(database, postToLedger: true);
        var journalCount = await database.Context.JournalEntries.CountAsync();
        var draft = (await service.AddAsync(DraftRequest(initialRate is null ? null : decimal.Parse(initialRate)))).Value;
        var patched = await service.UpdateDraftRateAsync(draft.Id,
            new CashVoucherDraftRateRequest(ExchangeRate: 52.25m, RowVersion: draft.RowVersion));
        Assert.True(patched.IsSuccess, patched.Error.Description);
        Assert.True(patched.Value.IsDraft);
        Assert.False(draft.RowVersion.SequenceEqual(patched.Value.RowVersion));
        Assert.Equal(52.25m, patched.Value.ExchangeRate);
        Assert.Equal(522.5m, patched.Value.BaseAmount);
        Assert.Equal(draft.Id, patched.Value.Id);
        Assert.Equal(draft.VoucherNumber, patched.Value.VoucherNumber);
        Assert.Equal(draft.FiscalYearId, patched.Value.FiscalYearId);
        Assert.Equal(draft.VoucherDate, patched.Value.VoucherDate);
        Assert.Equal(draft.CashboxId, patched.Value.CashboxId);
        Assert.Equal(draft.Direction, patched.Value.Direction);
        Assert.Equal(draft.Amount, patched.Value.Amount);
        Assert.Equal(draft.Currency, patched.Value.Currency);
        Assert.Equal(draft.Description, patched.Value.Description);
        Assert.Equal(CashPartyType.None, patched.Value.PartyType);
        Assert.Null(patched.Value.CashMovementTypeId);
        Assert.Null(patched.Value.PartnerAmount);
        Assert.Equal(100m, patched.Value.CashboxBalance);
        Assert.Equal(journalCount, await database.Context.JournalEntries.CountAsync());
        Assert.Empty(await database.Context.BusinessPartnerMovements.Where(x => x.CashVoucherId == draft.Id).ToListAsync());
        Assert.Empty(await database.Context.EmployeeMovements.Where(x => x.CashVoucherId == draft.Id).ToListAsync());
        Assert.False((await database.Context.CashVouchers.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).IsPosted);
        await AssertRatesUnchangedAsync(database);
        var handover = await service.GetHandoverReportAsync(new PaginationRequest());
        Assert.Equal(52.25m, Assert.Single(handover.Value.Items).ExchangeRate);
        Assert.Equal(522.5m, handover.Value.Items[0].BaseAmount);

        var completed = await service.UpdateAsync(draft.Id, CompletionRequest(patched.Value));
        Assert.True(completed.IsSuccess, completed.Error.Description);
        Assert.False(completed.Value.IsDraft);
        Assert.Equal(52.25m, completed.Value.ExchangeRate);
        Assert.Equal(522.5m, completed.Value.BaseAmount);
    }

    [Fact]
    public async Task SameRateRotatesVersionAndRejectsStaleVersionInTrackedContext()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = CreateService(database);
        var draft = (await service.AddAsync(DraftRequest(50m))).Value;
        var patch = new CashVoucherDraftRateRequest(ExchangeRate: 50m, RowVersion: draft.RowVersion);
        var first = await service.UpdateDraftRateAsync(draft.Id, patch);
        Assert.True(first.IsSuccess);
        Assert.False(draft.RowVersion.SequenceEqual(first.Value.RowVersion));
        var stale = await service.UpdateDraftRateAsync(draft.Id, patch with { ExchangeRate = 55m });
        Assert.Equal("CashVouchers.Concurrency", stale.Error.Code);
        Assert.Equal(50m, (await service.GetByIdAsync(draft.Id)).Value.ExchangeRate);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("0.0000000000001")]
    [InlineData("10000000000000000")]
    public async Task InvalidPatchRateIsRejectedWithoutChangingSnapshot(string rateText)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = CreateService(database);
        var draft = (await service.AddAsync(DraftRequest(50m))).Value;
        var request = new CashVoucherDraftRateRequest(
            ExchangeRate: decimal.Parse(rateText, CultureInfo.InvariantCulture), RowVersion: draft.RowVersion);
        Assert.False(new CashVoucherDraftRateRequestValidator().Validate(request).IsValid);
        var result = await service.UpdateDraftRateAsync(draft.Id, request);
        Assert.True(result.IsFailure);
        Assert.Equal("ExchangeRates.InvalidRate", result.Error.Code);
        Assert.Equal(50m, (await service.GetByIdAsync(draft.Id)).Value.ExchangeRate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7)]
    [InlineData(9)]
    public async Task MissingOrInvalidVersionIsRejected(int? length)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = CreateService(database);
        var draft = (await service.AddAsync(DraftRequest(50m))).Value;
        var request = new CashVoucherDraftRateRequest(ExchangeRate: 51m,
            RowVersion: length.HasValue ? new byte[length.Value] : null);
        Assert.False(new CashVoucherDraftRateRequestValidator().Validate(request).IsValid);
        Assert.Equal("CashVouchers.RowVersionRequired", (await service.UpdateDraftRateAsync(draft.Id, request)).Error.Code);
    }

    [Fact]
    public async Task PatchRequiresExecuteAndCannotAccessAnotherCompany()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = CreateService(database);
        var draft = (await service.AddAsync(DraftRequest(50m))).Value;
        var request = new CashVoucherDraftRateRequest(ExchangeRate: 51m, RowVersion: draft.RowVersion);
        var restricted = database.CreateVoucherService(companyId: 1,
            cashboxAccessService: new RestrictedCashboxAccess(view: [5], execute: []));
        Assert.Equal(ErrorType.Forbidden, (await restricted.UpdateDraftRateAsync(draft.Id, request)).Error.Type);
        Assert.Equal("CashVouchers.NotFound", (await database.CreateVoucherService(companyId: 2)
            .UpdateDraftRateAsync(draft.Id, request)).Error.Code);
        Assert.Equal(50m, (await service.GetByIdAsync(draft.Id)).Value.ExchangeRate);
    }

    [Fact]
    public async Task PostedVoucherCannotBePatched()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = CreateService(database);
        var draft = (await service.AddAsync(DraftRequest(50m))).Value;
        var posted = (await service.UpdateAsync(draft.Id, CompletionRequest(draft))).Value;
        var result = await service.UpdateDraftRateAsync(draft.Id,
            new CashVoucherDraftRateRequest(ExchangeRate: 51m, RowVersion: posted.RowVersion));
        Assert.Equal("CashVouchers.DraftRequired", result.Error.Code);
        Assert.Equal(50m, (await service.GetByIdAsync(draft.Id)).Value.ExchangeRate);
    }

    [Fact]
    public async Task ClosedYearRejectsPatch()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = CreateService(database);
        var draft = (await service.AddAsync(DraftRequest(50m))).Value;
        await database.Context.Database.ExecuteSqlRawAsync("UPDATE FiscalYears SET Status = 2 WHERE Id = 1");
        var result = await service.UpdateDraftRateAsync(draft.Id,
            new CashVoucherDraftRateRequest(ExchangeRate: 51m, RowVersion: draft.RowVersion));
        Assert.Equal("FiscalYears.Closed", result.Error.Code);
        Assert.Equal(50m, (await service.GetByIdAsync(draft.Id)).Value.ExchangeRate);
    }

    [Fact]
    public async Task DifferentSelectedYearRejectsPatchWithoutChangingDraft()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = CreateService(database);
        var draft = (await service.AddAsync(DraftRequest(50m))).Value;
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE FiscalYears SET IsCurrent = 0 WHERE CompanyId = 1;
            INSERT INTO FiscalYears (Id, CompanyId, Name, StartDate, EndDate, Status,
                IsCurrent, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (100, 1, '2027', '2027-01-01', '2027-12-31', 1,
                1, 'test', '2026-01-01', 'test', 0);
            """);
        var result = await service.UpdateDraftRateAsync(draft.Id,
            new CashVoucherDraftRateRequest(ExchangeRate: 51m, RowVersion: draft.RowVersion));
        Assert.Equal("FiscalYears.QueryDateOutsideRange", result.Error.Code);
        var stored = await database.Context.CashVouchers.AsNoTracking().SingleAsync(x => x.Id == draft.Id);
        Assert.Equal(50m, stored.ExchangeRate);
        Assert.False(stored.IsPosted);
    }

    [Fact]
    public async Task TransferGeneratedVoucherRejectsDraftRatePatch()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var transfer = await database.CreateCashboxTransferService(1).AddAsync(
            new MiniErp.Application.Features.CashboxTransfers.CashboxTransferRequest(
                TransferDate: new DateOnly(2026, 8, 1), SourceCashboxId: 1,
                DestinationCashboxId: 2, Amount: 10m, Description: "Generated voucher", Notes: null));
        Assert.True(transfer.IsSuccess, transfer.Error.Description);
        var service = CreateService(database);
        var generated = (await service.GetByIdAsync(transfer.Value.PaymentVoucherId)).Value;
        var result = await service.UpdateDraftRateAsync(generated.Id,
            new CashVoucherDraftRateRequest(ExchangeRate: 1m, RowVersion: generated.RowVersion));
        Assert.Equal("CashVouchers.TransferGeneratedReadOnly", result.Error.Code);
    }

    [Theory]
    [InlineData(0, "CashVouchers.InvalidId")]
    [InlineData(-1, "CashVouchers.InvalidId")]
    [InlineData(999999, "CashVouchers.NotFound")]
    public async Task InvalidOrMissingVoucherIsRejected(int id, string expectedCode)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var result = await CreateService(database).UpdateDraftRateAsync(id,
            new CashVoucherDraftRateRequest(ExchangeRate: 51m, RowVersion: new byte[8]));
        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Fact]
    public void DraftRateEndpointIsPatchNotCompletionPut()
    {
        var action = typeof(MiniErp.Api.Controllers.CashVouchersController).GetMethod("UpdateDraftRate")!;
        var route = Assert.Single(action.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPatchAttribute), false)
            .Cast<Microsoft.AspNetCore.Mvc.HttpPatchAttribute>());
        Assert.Equal("{id:int}/draft-rate", route.Template);
    }

    [Fact]
    public async Task BaseCurrencyRequiresOne()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = CreateService(database);
        var draft = (await service.AddAsync(DraftRequest(null) with { CashboxId = 1 })).Value;
        var bad = await service.UpdateDraftRateAsync(draft.Id,
            new CashVoucherDraftRateRequest(ExchangeRate: 2m, RowVersion: draft.RowVersion));
        Assert.Equal("ExchangeRates.BaseCurrencyRateMustBeOne", bad.Error.Code);
        var good = await service.UpdateDraftRateAsync(draft.Id,
            new CashVoucherDraftRateRequest(ExchangeRate: 1m, RowVersion: draft.RowVersion));
        Assert.True(good.IsSuccess);
        Assert.True(good.Value.IsDraft);
        Assert.Equal(1m, good.Value.ExchangeRate);
        Assert.Equal(10m, good.Value.BaseAmount);
    }

    [Fact]
    public async Task BaseAmountStorageOverflowIsValidationNotServerError()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = CreateService(database);
        var draft = (await service.AddAsync(DraftRequest(null) with { Amount = 1_000_000_000m })).Value;
        var result = await service.UpdateDraftRateAsync(draft.Id,
            new CashVoucherDraftRateRequest(ExchangeRate: 1_000_000_000_000m, RowVersion: draft.RowVersion));
        Assert.Equal("CashVouchers.BaseAmountOutOfRange", result.Error.Code);
        Assert.Equal(0m, (await service.GetByIdAsync(draft.Id)).Value.BaseAmount);
    }

    [Fact]
    public async Task ManualDraftRateIsVisibleOnEveryReadWithoutFinancialEffects()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SeedRatesAsync(database);
        var service = CreateService(database, postToLedger: true);
        var originalJournalCount = await database.Context.JournalEntries.CountAsync();
        var receipt = await service.AddAsync(DraftRequest(rate: 50.25m));
        var payment = await service.AddAsync(DraftRequest(rate: 51m) with
        {
            Direction = CashDirection.Payment,
            Amount = 150m,
            VoucherDate = new DateOnly(2026, 8, 2)
        });

        Assert.True(receipt.IsSuccess, receipt.Error.Description);
        Assert.True(payment.IsSuccess, payment.Error.Description);
        Assert.True(receipt.Value.IsDraft);
        Assert.Equal(10m, receipt.Value.Amount);
        Assert.Equal(CurrencyCode.USD, receipt.Value.Currency);
        Assert.Equal(CurrencyCode.EGP, receipt.Value.BaseCurrency);
        Assert.Equal(50.25m, receipt.Value.ExchangeRate);
        Assert.Equal(502.5m, receipt.Value.BaseAmount);
        Assert.Equal(100m, receipt.Value.CashboxBalance);
        Assert.Equal(7_650m, payment.Value.BaseAmount);
        var stored = await database.Context.CashVouchers.AsNoTracking()
            .SingleAsync(voucher => voucher.Id == receipt.Value.Id);
        Assert.Null(stored.ExchangeRateId);
        Assert.Equal(50.25m, stored.ExchangeRate);
        Assert.Equal(502.5m, stored.BaseAmount);
        var byId = await service.GetByIdAsync(receipt.Value.Id);
        Assert.Equal(50.25m, byId.Value.ExchangeRate);
        Assert.Equal(502.5m, byId.Value.BaseAmount);

        for (var page = 1; page <= 2; page++)
        {
            var expected = page == 1 ? payment.Value : receipt.Value;
            var pagination = new PaginationRequest { PageNumber = page, PageSize = 1 };
            var list = await service.GetAllAsync(pagination,
                new CashVoucherFilterRequest(IsDraft: true));
            var listItem = Assert.Single(list.Value.Items);
            Assert.Equal(expected.Id, listItem.Id);
            Assert.Equal(expected.Amount, listItem.Amount);
            Assert.Equal(expected.ExchangeRate, listItem.ExchangeRate);
            Assert.Equal(expected.BaseAmount, listItem.BaseAmount);
            var report = await service.GetHandoverReportAsync(pagination);
            var reportItem = Assert.Single(report.Value.Items);
            Assert.Equal(expected.Id, reportItem.Id);
            Assert.Equal(expected.Amount, reportItem.Amount);
            Assert.Equal(CurrencyCode.USD, reportItem.Currency);
            Assert.Equal(CurrencyCode.EGP, reportItem.BaseCurrency);
            Assert.Equal(expected.ExchangeRate, reportItem.ExchangeRate);
            Assert.Equal(expected.BaseAmount, reportItem.BaseAmount);
            var summary = Assert.Single(report.Value.Summaries);
            Assert.Equal(10m, summary.Receipt);
            Assert.Equal(150m, summary.Payment);
            Assert.Equal(-140m, summary.Net);
            var balance = Assert.Single(report.Value.CashboxBalances);
            Assert.Equal(100m, balance.CurrentBalance);
            Assert.Equal(10m, balance.DraftReceipt);
            Assert.Equal(150m, balance.DraftPayment);
            Assert.Equal(-40m, balance.ExpectedBalance);
        }

        Assert.Equal(100m, (await database.CreateCashboxService(1).GetByIdAsync(5))
            .Value.CurrentBalance);
        Assert.Equal(originalJournalCount, await database.Context.JournalEntries.CountAsync());
        Assert.Empty(await database.Context.BusinessPartnerMovements
            .Where(movement => movement.CashVoucherId == receipt.Value.Id ||
                movement.CashVoucherId == payment.Value.Id)
            .ToListAsync());
        Assert.Empty(await database.Context.EmployeeMovements
            .Where(movement => movement.CashVoucherId == receipt.Value.Id ||
                movement.CashVoucherId == payment.Value.Id)
            .ToListAsync());
        await AssertRatesUnchangedAsync(database);
    }

    [Theory]
    [InlineData("50.25", false)]
    [InlineData("50.25", true)]
    [InlineData("1", false)]
    [InlineData("1", true)]
    [InlineData("0.000000000001", false)]
    public async Task CompletionPreservesCapturedRateUnlessExplicitlyOverridden(
        string draftRateText, bool overrideRate)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SeedRatesAsync(database);
        var draftRate = decimal.Parse(draftRateText, CultureInfo.InvariantCulture);
        // Tiny valid snapshots can round to zero base amount. Use the service's
        // no-op posting double for that case to isolate preservation from the
        // ledger's minimum posting amount validation.
        var service = CreateService(database, postToLedger: draftRate >= 1m);
        var draft = await service.AddAsync(DraftRequest(draftRate));
        Assert.True(draft.IsSuccess, draft.Error.Description);
        var request = CompletionRequest(draft.Value) with
        {
            Amount = 20m,
            ExchangeRate = overrideRate ? 52m : null
        };
        var completed = await service.UpdateAsync(draft.Value.Id, request);

        Assert.True(completed.IsSuccess, completed.Error.Description);
        var expectedRate = overrideRate ? 52m : draftRate;
        var expectedBaseAmount = ExchangeRateRules.ConvertToBase(20m, expectedRate);
        Assert.False(completed.Value.IsDraft);
        Assert.Equal(expectedRate, completed.Value.ExchangeRate);
        Assert.Equal(20m, completed.Value.Amount);
        Assert.Equal(expectedBaseAmount, completed.Value.BaseAmount);
        Assert.Equal(120m, (await database.CreateCashboxService(1).GetByIdAsync(5))
            .Value.CurrentBalance);
        if (draftRate >= 1m)
        {
            var lines = await database.Context.JournalEntryLines.AsNoTracking()
                .Where(line => line.JournalEntry.SourceType == JournalEntrySourceType.CashVoucher &&
                    line.JournalEntry.SourceId == draft.Value.Id)
                .ToListAsync();
            var cashboxLine = Assert.Single(lines, line => line.PartyType == JournalPartyType.Cashbox);
            Assert.Equal(expectedRate, cashboxLine.ExchangeRate);
            Assert.Equal(expectedBaseAmount, cashboxLine.Debit);
            Assert.Equal(20m, cashboxLine.TransactionDebit);
        }
        await AssertRatesUnchangedAsync(database);
    }

    [Theory]
    [InlineData("0", 5, "ExchangeRates.InvalidRate")]
    [InlineData("-2", 5, "ExchangeRates.InvalidRate")]
    [InlineData("0.0000000000001", 5, "ExchangeRates.InvalidRate")]
    [InlineData("2", 1, "ExchangeRates.BaseCurrencyRateMustBeOne")]
    public async Task InvalidDraftRateFailsWithoutSaving(
        string rateText, int cashboxId, string errorCode)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var request = DraftRequest(decimal.Parse(rateText, CultureInfo.InvariantCulture)) with
        {
            CashboxId = cashboxId
        };
        var validation = new CashVoucherRequestValidator().Validate(request);
        if (cashboxId == 5)
        {
            Assert.Contains(validation.Errors,
                error => error.PropertyName == nameof(CashVoucherRequest.ExchangeRate));
        }
        var result = await CreateService(database).AddAsync(request);
        Assert.True(result.IsFailure);
        Assert.Equal(errorCode, result.Error.Code);
        Assert.Empty(await database.Context.CashVouchers.ToListAsync());
    }

    [Fact]
    public async Task BaseCurrencyDraftAcceptsOneAndUnpricedForeignDraftNeedsNoRegisteredRate()
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        var service = CreateService(database);
        var baseDraft = await service.AddAsync(DraftRequest(rate: 1m) with { CashboxId = 1 });
        var foreignDraft = await service.AddAsync(DraftRequest(rate: null));
        Assert.True(baseDraft.IsSuccess, baseDraft.Error.Description);
        Assert.Equal(1m, baseDraft.Value.ExchangeRate);
        Assert.Equal(10m, baseDraft.Value.BaseAmount);
        Assert.True(foreignDraft.IsSuccess, foreignDraft.Error.Description);
        Assert.Equal(CurrencyCode.USD, foreignDraft.Value.Currency);
        Assert.Equal(1m, foreignDraft.Value.ExchangeRate);
        Assert.Equal(0m, foreignDraft.Value.BaseAmount);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("currency")]
    [InlineData("date")]
    [InlineData("posted")]
    public async Task CompletionResolvesRegisteredRateWhenDraftSnapshotDoesNotApply(string scenario)
    {
        await using var database = await CashManagementTestDatabase.CreateAsync();
        await SeedRatesAsync(database);
        var service = CreateService(database);
        var draft = await service.AddAsync(DraftRequest(scenario == "legacy" ? null : 50m));
        Assert.True(draft.IsSuccess, draft.Error.Description);
        var request = CompletionRequest(draft.Value) with
        {
            CashboxId = scenario == "currency" ? 6 : 5,
            VoucherDate = scenario == "date"
                ? draft.Value.VoucherDate.AddDays(1)
                : draft.Value.VoucherDate
        };
        var completed = await service.UpdateAsync(draft.Value.Id, request);
        Assert.True(completed.IsSuccess, completed.Error.Description);
        if (scenario == "posted")
        {
            Assert.Equal(50m, completed.Value.ExchangeRate);
            database.Context.ChangeTracker.Clear();
            completed = await service.UpdateAsync(completed.Value.Id, CompletionRequest(completed.Value));
            Assert.True(completed.IsSuccess, completed.Error.Description);
        }
        var expectedRate = scenario == "currency" ? 60m : 40m;
        Assert.Equal(expectedRate, completed.Value.ExchangeRate);
        Assert.Equal(10m * expectedRate, completed.Value.BaseAmount);
        Assert.Equal(scenario == "currency" ? CurrencyCode.EUR : CurrencyCode.USD,
            completed.Value.Currency);
        var stored = await database.Context.CashVouchers.AsNoTracking()
            .SingleAsync(voucher => voucher.Id == draft.Value.Id);
        Assert.Equal(scenario == "currency" ? 2 : 1, stored.ExchangeRateId);
        await AssertRatesUnchangedAsync(database);
    }

    private static CashVoucherService CreateService(CashManagementTestDatabase database,
        bool postToLedger = false)
    {
        var resolver = new ExchangeRateResolver(database.Context,
            new TestCurrentCompanyContext(1), TimeProvider.System);
        return postToLedger
            ? database.CreatePostingVoucherService(companyId: 1, exchangeRateResolver: resolver)
            : database.CreateVoucherService(companyId: 1, exchangeRateResolver: resolver);
    }

    private static CashVoucherRequest DraftRequest(decimal? rate) => new(
        VoucherDate: new DateOnly(2026, 8, 1),
        Direction: CashDirection.Receipt,
        CashboxId: 5,
        Amount: 10m,
        Description: "Manual handover rate",
        ExchangeRate: rate);

    private static CashVoucherUpdateRequest CompletionRequest(CashVoucherResponse draft) => new(
        VoucherDate: draft.VoucherDate,
        Direction: draft.Direction,
        CashboxId: draft.CashboxId,
        CashMovementTypeId: null,
        EmployeeId: null,
        BusinessPartnerId: null,
        DriverId: null,
        DriverTripId: null,
        ExternalPartyName: null,
        Amount: draft.Amount,
        ReferenceNumber: null,
        Description: draft.Description,
        Notes: null,
        RowVersion: draft.RowVersion,
        AccountId: 1);

    private static Task SeedRatesAsync(CashManagementTestDatabase database) =>
        database.Context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE ExchangeRates (
                Id INTEGER PRIMARY KEY,
                CompanyId INTEGER NOT NULL,
                FiscalYearId INTEGER NOT NULL,
                Currency INTEGER NOT NULL,
                RateDate TEXT NOT NULL,
                Rate NUMERIC NOT NULL,
                Source INTEGER NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );
            INSERT INTO ExchangeRates (Id, CompanyId, FiscalYearId, Currency, RateDate, Rate, Source)
            VALUES (1, 1, 1, 2, '2026-07-31', 40, 1),
                   (2, 1, 1, 3, '2026-07-31', 60, 1);
            """);

    private static async Task AssertRatesUnchangedAsync(CashManagementTestDatabase database)
    {
        var rates = await database.Context.ExchangeRates.AsNoTracking()
            .OrderBy(rate => rate.Id)
            .Select(rate => new { rate.Id, rate.Currency, rate.Rate, rate.RateDate })
            .ToListAsync();
        Assert.Equal(2, rates.Count);
        Assert.Equal(40m, rates[0].Rate);
        Assert.Equal(CurrencyCode.USD, rates[0].Currency);
        Assert.Equal(60m, rates[1].Rate);
        Assert.Equal(CurrencyCode.EUR, rates[1].Currency);
        Assert.All(rates, rate => Assert.Equal(new DateOnly(2026, 7, 31), rate.RateDate));
    }

    private sealed record TestCurrentCompanyContext(int CompanyId) : ICurrentCompanyContext;
}
