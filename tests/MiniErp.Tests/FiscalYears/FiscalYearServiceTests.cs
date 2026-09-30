using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.Companies;
using MiniErp.Application.Common.Results;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.FiscalYears;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.FiscalYears;

public sealed class FiscalYearServiceTests
{
    static FiscalYearServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task Add_TrimsNameAndMakesFirstYearCurrent()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var result = await service.AddAsync(
            new FiscalYearRequest(
                "  2026  ",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));

        Assert.True(result.IsSuccess);
        Assert.Equal("2026", result.Value.Name);
        Assert.Equal(FiscalYearStatus.Open, result.Value.Status);
        Assert.True(result.Value.IsCurrent);
    }

    [Fact]
    public async Task Add_CarriesLatestExchangeRatesToFirstDayOfNewYear()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var previous = await service.AddAsync(
            new FiscalYearRequest(
                "2025",
                new DateOnly(2025, 1, 1),
                new DateOnly(2025, 12, 31)));
        await database.SeedExchangeRateAsync(
            previous.Value.Id,
            new DateOnly(2025, 12, 31),
            50m);

        var next = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                IsCurrent: true));

        var carried = await database.Context.ExchangeRates
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(rate =>
                rate.CompanyId == 1 &&
                rate.Currency == CurrencyCode.USD &&
                rate.FiscalYearId == next.Value.Id);

        Assert.Equal(new DateOnly(2026, 1, 1), carried.RateDate);
        Assert.Equal(50m, carried.Rate);
    }

    [Fact]
    public async Task Add_RejectsOverlappingPeriodInSameCompany()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);

        var first = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var overlapping = await service.AddAsync(
            new FiscalYearRequest(
                "2026/2027",
                new DateOnly(2026, 12, 1),
                new DateOnly(2027, 11, 30),
                IsCurrent: false));

        Assert.True(first.IsSuccess);
        Assert.True(overlapping.IsFailure);
        Assert.Equal(
            "FiscalYears.DateRangeOverlaps",
            overlapping.Error.Code);
    }

    [Fact]
    public async Task CurrentYear_IsScopedPerCompanyAndCanBeSwitched()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var companyOne = database.CreateService(companyId: 1);
        var companyTwo = database.CreateService(companyId: 2);

        var first = await companyOne.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var second = await companyOne.AddAsync(
            new FiscalYearRequest(
                "2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                IsCurrent: true));
        var otherCompany = await companyTwo.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(otherCompany.IsSuccess);
        Assert.False((await companyOne.GetByIdAsync(first.Value.Id)).Value.IsCurrent);
        Assert.True((await companyOne.GetCurrentAsync()).Value.Id == second.Value.Id);
        Assert.True((await companyTwo.GetCurrentAsync()).Value.Id == otherCompany.Value.Id);
    }

    [Fact]
    public async Task SetCurrent_SwitchesCompanyContextAndAllowsHistoricalClosedYear()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var companyOne = database.CreateService(companyId: 1);
        var companyTwo = database.CreateService(companyId: 2);
        var historical = await companyOne.AddAsync(
            new FiscalYearRequest(
                "2025",
                new DateOnly(2025, 1, 1),
                new DateOnly(2025, 12, 31)));
        var current = await companyOne.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                IsCurrent: true));
        Assert.True(historical.IsSuccess);
        Assert.True(current.IsSuccess);
        await database.Context.FiscalYears
            .Where(year => year.Id == historical.Value.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                year => year.Status,
                FiscalYearStatus.Closed));
        database.ClearTracking();

        var switched = await companyOne.SetCurrentAsync(historical.Value.Id);

        Assert.True(switched.IsSuccess);
        Assert.True(switched.Value.IsCurrent);
        Assert.Equal(FiscalYearStatus.Closed, switched.Value.Status);
        Assert.False((await companyOne.GetByIdAsync(current.Value.Id))
            .Value.IsCurrent);
        Assert.Equal(
            historical.Value.Id,
            (await companyOne.GetCurrentAsync()).Value.Id);

        var crossCompany = await companyTwo.SetCurrentAsync(
            historical.Value.Id);
        Assert.True(crossCompany.IsFailure);
        Assert.Equal("FiscalYears.NotFound", crossCompany.Error.Code);
    }

    [Fact]
    public async Task Update_UsesRowVersionAndClosedYearCannotBeModified()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var added = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var next = await service.AddAsync(
            new FiscalYearRequest(
                "2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                IsCurrent: false));

        var closed = await service.CloseAsync(added.Value.Id);
        var update = await service.UpdateAsync(
            added.Value.Id,
            new FiscalYearUpdateRequest(
                "2026 Updated",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                true,
                closed.Value.RowVersion));

        Assert.True(closed.IsSuccess);
        Assert.Equal(FiscalYearStatus.Closed, closed.Value.Status);
        Assert.True(update.IsFailure);
        Assert.Equal(
            "FiscalYears.ClosedCannotBeModified",
            update.Error.Code);
    }

    [Fact]
    public async Task Close_BlocksWhenAccountingReadinessHasIssues()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(
            companyId: 1,
            accountingReadinessService: new BlockedReadinessService());
        var added = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        await service.AddAsync(
            new FiscalYearRequest(
                "2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                IsCurrent: false));

        var close = await service.CloseAsync(added.Value.Id);

        Assert.True(close.IsFailure);
        Assert.Equal("FiscalYears.ClosingNotReady", close.Error.Code);
        Assert.Equal(
            FiscalYearStatus.Open,
            (await service.GetByIdAsync(added.Value.Id)).Value.Status);
    }

    [Fact]
    public async Task Close_TransfersFinancialPositionOnceAcrossReopen()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(
            companyId: 1,
            accountingReadinessService: new ReadyReadinessService());
        var first = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var next = await service.AddAsync(
            new FiscalYearRequest(
                "2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                IsCurrent: true));
        await database.SeedClosingLedgerAsync(
            first.Value.Id,
            next.Value.Id);
        database.ClearTracking();

        Assert.True((await service.CloseAsync(first.Value.Id)).IsSuccess);
        Assert.Equal(2, (await service.GetSelectAsync()).Value.Count);
        database.ClearTracking();
        Assert.True((await service.ReopenAsync(first.Value.Id)).IsSuccess);
        Assert.Empty(await database.LoadClosingTransfersAsync(first.Value.Id));
        await database.ChangeClosingAssetBalanceAsync(120m);
        database.ClearTracking();
        Assert.True((await service.CloseAsync(first.Value.Id)).IsSuccess);

        var transfers = await database.LoadClosingTransfersAsync(
            first.Value.Id);
        Assert.Single(transfers);
        Assert.Equal(next.Value.Id, transfers[0].FiscalYearId);
        Assert.Equal(120m, transfers[0].Debit);
        Assert.Equal(120m, transfers[0].Credit);
        var partyLine = await database.LoadClosingPartyLineAsync(
            first.Value.Id);
        Assert.Equal(JournalPartyType.Customer, partyLine.PartyType);
        Assert.Equal(99, partyLine.PartyId);
        Assert.Equal(CurrencyCode.USD, partyLine.Currency);
        Assert.Equal(50m, partyLine.TransactionDebit);
        Assert.Equal(0m, partyLine.TransactionCredit);
        Assert.Equal(2.4m, partyLine.ExchangeRate);
    }

    [Fact]
    public async Task Close_CarriesDriverBalanceAsNextYearOpening()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(
            companyId: 1,
            accountingReadinessService: new ReadyReadinessService());
        var first = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var next = await service.AddAsync(
            new FiscalYearRequest(
                "2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                IsCurrent: true));
        await database.SeedClosingLedgerAsync(
            first.Value.Id,
            next.Value.Id,
            partyType: JournalPartyType.Driver,
            partyId: 7);
        database.ClearTracking();

        var close = await service.CloseAsync(first.Value.Id);

        Assert.True(close.IsSuccess);
        var partyLine = await database.LoadClosingPartyLineAsync(first.Value.Id);
        Assert.Equal(JournalPartyType.Driver, partyLine.PartyType);
        Assert.Equal(7, partyLine.PartyId);
        Assert.Equal(100m, partyLine.Debit);
        Assert.Equal(0m, partyLine.Credit);
        Assert.Equal(CurrencyCode.USD, partyLine.Currency);
        Assert.Equal(50m, partyLine.TransactionDebit);
        Assert.Equal(0m, partyLine.TransactionCredit);
    }

    [Fact]
    public async Task Close_CarriesOnlyEmployeeOperationalDelta_AndRecloseIsIdempotent()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(
            companyId: 1,
            accountingReadinessService: new ReadyReadinessService());
        var first = await service.AddAsync(new FiscalYearRequest(
            "2026",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31)));
        var next = await service.AddAsync(new FiscalYearRequest(
            "2027",
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 12, 31),
            IsCurrent: true));
        await database.SeedClosingLedgerAsync(first.Value.Id, next.Value.Id);
        await database.SeedEmployeeOperationalBalancesAsync(
            first.Value.Id,
            next.Value.Id);
        database.ClearTracking();

        Assert.True((await service.CloseAsync(first.Value.Id)).IsSuccess);
        var firstLines = await database.LoadClosingEmployeeLinesAsync(
            first.Value.Id);

        Assert.Equal(-750m, firstLines.Sum(line => line.Debit - line.Credit));
        var delta = Assert.Single(
            firstLines,
            line => line.AccountId == 50 && line.Credit == 800m);
        Assert.Equal(0m, delta.Debit);
        Assert.Equal(800m, delta.Credit);
        Assert.Equal(7, delta.PartyId);

        database.ClearTracking();
        Assert.True((await service.ReopenAsync(first.Value.Id)).IsSuccess);
        database.ClearTracking();
        Assert.True((await service.CloseAsync(first.Value.Id)).IsSuccess);
        var reclosedLines = await database.LoadClosingEmployeeLinesAsync(
            first.Value.Id);

        Assert.Equal(firstLines.Count, reclosedLines.Count);
        Assert.Equal(-750m, reclosedLines.Sum(line => line.Debit - line.Credit));
        Assert.Single(
            reclosedLines,
            line => line.AccountId == 50 && line.Credit == 800m);
    }

    [Fact]
    public async Task Reopen_BlocksWhenLaterYearIsClosedAndPreservesTransfer()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(
            companyId: 1,
            accountingReadinessService: new ReadyReadinessService());
        var first = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var next = await service.AddAsync(
            new FiscalYearRequest(
                "2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                IsCurrent: true));
        await service.AddAsync(
            new FiscalYearRequest(
                "2028",
                new DateOnly(2028, 1, 1),
                new DateOnly(2028, 12, 31),
                IsCurrent: false));
        await database.SeedClosingLedgerAsync(
            first.Value.Id,
            next.Value.Id);
        database.ClearTracking();

        Assert.True((await service.CloseAsync(first.Value.Id)).IsSuccess);
        database.ClearTracking();
        Assert.True((await service.CloseAsync(next.Value.Id)).IsSuccess);
        database.ClearTracking();

        var reopen = await service.ReopenAsync(first.Value.Id);

        Assert.True(reopen.IsFailure);
        Assert.Equal("FiscalYears.LaterFiscalYearClosed", reopen.Error.Code);
        Assert.Equal(
            FiscalYearStatus.Closed,
            (await service.GetByIdAsync(first.Value.Id)).Value.Status);
        Assert.Equal(
            FiscalYearStatus.Closed,
            (await service.GetByIdAsync(next.Value.Id)).Value.Status);
        Assert.Single(await database.LoadClosingTransfersAsync(first.Value.Id));
    }

    [Fact]
    public async Task Close_CreatesAndPromotesNextFiscalYearWhenMissing()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var setup = new CapturingAccountingSetupService();
        var inventoryCarry = new CapturingInventoryCarryForwardService();
        var service = database.CreateService(
            companyId: 1,
            defaultAccountingSetupService: setup,
            inventoryCarryForwardService: inventoryCarry);
        var current = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));

        var result = await service.CloseAsync(current.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(FiscalYearStatus.Closed, result.Value.Status);
        Assert.False(result.Value.IsCurrent);
        var next = (await service.GetCurrentAsync()).Value;
        Assert.Equal("2027", next.Name);
        Assert.Equal(new DateOnly(2027, 1, 1), next.StartDate);
        Assert.Equal(new DateOnly(2027, 12, 31), next.EndDate);
        Assert.Equal(FiscalYearStatus.Open, next.Status);
        Assert.Contains((1, next.Id), setup.EnsuredFiscalYears);
        Assert.Equal(
            [(current.Value.Id, next.Id, next.StartDate, current.Value.Name)],
            inventoryCarry.Calls);
    }

    [Fact]
    public async Task Close_CreatesImmediateNextYearWhenALaterYearExists()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var setup = new CapturingAccountingSetupService();
        var service = database.CreateService(
            companyId: 1,
            defaultAccountingSetupService: setup);
        var current = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var later = await service.AddAsync(
            new FiscalYearRequest(
                "2028",
                new DateOnly(2028, 1, 1),
                new DateOnly(2028, 12, 31),
                IsCurrent: false));

        var result = await service.CloseAsync(current.Value.Id);

        Assert.True(result.IsSuccess);
        var next = (await service.GetCurrentAsync()).Value;
        Assert.Equal("2027", next.Name);
        Assert.Equal(new DateOnly(2027, 1, 1), next.StartDate);
        Assert.Equal(new DateOnly(2027, 12, 31), next.EndDate);
        Assert.NotEqual(later.Value.Id, next.Id);
        Assert.Equal(3, (await service.GetSelectAsync()).Value.Count);
        Assert.Contains((1, next.Id), setup.EnsuredFiscalYears);
    }

    [Fact]
    public async Task Close_PromotesNextYearAndReopenKeepsItCurrent()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var added = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        var next = await service.AddAsync(
            new FiscalYearRequest(
                "2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                IsCurrent: false));

        var closed = await service.CloseAsync(added.Value.Id);
        database.ClearTracking();
        var reopened = await service.ReopenAsync(added.Value.Id);

        Assert.True(closed.IsSuccess);
        Assert.False(closed.Value.IsCurrent);
        Assert.Equal(next.Value.Id, (await service.GetCurrentAsync()).Value.Id);
        Assert.True(reopened.IsSuccess);
        Assert.Equal(FiscalYearStatus.Open, reopened.Value.Status);
        Assert.Null(reopened.Value.ClosedOn);
        Assert.False(reopened.Value.IsCurrent);
    }

    [Fact]
    public async Task OtherCompanyYear_IsNotVisibleOrMutable()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var otherCompany = database.CreateService(companyId: 2);
        var added = await otherCompany.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));

        var get = await service.GetByIdAsync(added.Value.Id);
        var close = await service.CloseAsync(added.Value.Id);
        var delete = await service.DeleteAsync(added.Value.Id);

        Assert.Equal("FiscalYears.NotFound", get.Error.Code);
        Assert.Equal("FiscalYears.NotFound", close.Error.Code);
        Assert.Equal("FiscalYears.NotFound", delete.Error.Code);
    }

    [Fact]
    public void UpdateValidator_RequiresEightByteRowVersion()
    {
        var validator = new FiscalYearUpdateRequestValidator();
        var result = validator.Validate(
            new FiscalYearUpdateRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                true,
                [1, 2]));

        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(FiscalYearUpdateRequest.RowVersion));
    }

    [Fact]
    public async Task PeriodGuard_AllowsOpenYearAndRejectsClosedOrUncoveredDates()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var guard = database.CreateGuard(companyId: 1);

        var uncovered = await guard.EnsureOpenAsync(
            new DateOnly(2025, 12, 31),
            "InvoiceDate");

        var added = await service.AddAsync(
            new FiscalYearRequest(
                "2026",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)));
        await service.AddAsync(
            new FiscalYearRequest(
                "2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                IsCurrent: false));
        var open = await guard.EnsureOpenAsync(
            new DateOnly(2026, 6, 1),
            "InvoiceDate");
        var otherOpenYear = await guard.EnsureOpenAsync(
            new DateOnly(2027, 6, 1),
            "InvoiceDate");

        await service.CloseAsync(added.Value.Id);
        database.Context.ChangeTracker.Clear();
        var selectedClosed = await service.SetCurrentAsync(added.Value.Id);
        var closed = await guard.EnsureOpenAsync(
            new DateOnly(2026, 6, 1),
            "InvoiceDate");

        Assert.True(uncovered.IsFailure);
        Assert.Equal("FiscalYears.DateNotCovered", uncovered.Error.Code);
        Assert.True(added.IsSuccess);
        Assert.True(open.IsSuccess);
        Assert.True(otherOpenYear.IsFailure);
        Assert.Equal(
            "FiscalYears.QueryDateOutsideRange",
            otherOpenYear.Error.Code);
        Assert.Equal("InvoiceDate", otherOpenYear.Error.FieldName);
        Assert.True(selectedClosed.IsSuccess);
        Assert.True(closed.IsFailure);
        Assert.Equal("FiscalYears.Closed", closed.Error.Code);
        Assert.Equal("InvoiceDate", closed.Error.FieldName);
    }

    private sealed class BlockedReadinessService : IAccountingReadinessService
    {
        public Task<Result<AccountingReadinessResponse>> GetAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                    Result<AccountingReadinessResponse>.Success(
                        new AccountingReadinessResponse(
                        FiscalYearId: fiscalYearId,
                        FiscalYearName: "2026",
                        StartDate: new DateOnly(2026, 1, 1),
                        EndDate: new DateOnly(2026, 12, 31),
                        IsReady: false,
                        TotalSources: 1,
                        PostedSources: 0,
                        MissingJournalSources: 1,
                        OrphanAutomaticJournals: 0,
                        DuplicateAutomaticJournals: 0,
                        UnbalancedAutomaticJournals: 0,
                        PendingInventoryCosts: 0,
                        MissingOrInvalidMappings: 0,
                        DeferredPayrollSources: 0,
                        Sources: [],
                        Issues: [new AccountingReadinessIssue(
                            IssueType: "MissingJournal",
                            SourceType: null,
                            SourceId: null,
                            SourceNumber: null,
                            SourceDate: null,
                            MappingType: null,
                            MappingSourceId: null,
                            Message: "missing")])));

        public Task<Result<AccountingBackfillResponse>> BackfillAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task QueryScopeResolver_UsesCurrentYearWhenIdIsOmitted()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var first = await service.AddAsync(
            new FiscalYearRequest(
                Name: "2025",
                StartDate: new DateOnly(2025, 1, 1),
                EndDate: new DateOnly(2025, 12, 31)));
        var current = await service.AddAsync(
            new FiscalYearRequest(
                Name: "2026",
                StartDate: new DateOnly(2026, 1, 1),
                EndDate: new DateOnly(2026, 12, 31),
                IsCurrent: true));
        var resolver = database.CreateQueryScopeResolver(companyId: 1);

        var result = await resolver.ResolveAsync(fiscalYearId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(current.Value.Id, result.Value.FiscalYearId);
        Assert.Equal("2026", result.Value.FiscalYearName);
        Assert.Equal(new DateOnly(2026, 1, 1), result.Value.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), result.Value.EndDate);
        Assert.Equal(FiscalYearStatus.Open, result.Value.Status);
        Assert.True(result.Value.IsCurrent);
        Assert.NotEqual(first.Value.Id, result.Value.FiscalYearId);
    }

    [Fact]
    public async Task QueryScopeResolver_AllowsExplicitHistoricalYear()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var historical = await service.AddAsync(
            new FiscalYearRequest(
                Name: "2025",
                StartDate: new DateOnly(2025, 1, 1),
                EndDate: new DateOnly(2025, 12, 31)));
        await service.AddAsync(
            new FiscalYearRequest(
                Name: "2026",
                StartDate: new DateOnly(2026, 1, 1),
                EndDate: new DateOnly(2026, 12, 31),
                IsCurrent: true));
        var resolver = database.CreateQueryScopeResolver(companyId: 1);

        var result = await resolver.ResolveAsync(
            fiscalYearId: historical.Value.Id,
            fromDate: new DateOnly(2025, 2, 1),
            toDate: new DateOnly(2025, 11, 30));

        Assert.True(result.IsSuccess);
        Assert.Equal(historical.Value.Id, result.Value.FiscalYearId);
        Assert.Equal("2025", result.Value.FiscalYearName);
        Assert.False(result.Value.IsCurrent);
    }

    [Fact]
    public async Task QueryScopeResolver_RejectsForeignCompanyYear()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var otherCompanyService = database.CreateService(companyId: 2);
        var foreignYear = await otherCompanyService.AddAsync(
            new FiscalYearRequest(
                Name: "2026",
                StartDate: new DateOnly(2026, 1, 1),
                EndDate: new DateOnly(2026, 12, 31)));
        var resolver = database.CreateQueryScopeResolver(companyId: 1);

        var result = await resolver.ResolveAsync(foreignYear.Value.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("FiscalYears.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task QueryScopeResolver_RejectsDatesOutsideSelectedYear()
    {
        await using var database = await FiscalYearTestDatabase.CreateAsync();
        var service = database.CreateService(companyId: 1);
        var year = await service.AddAsync(
            new FiscalYearRequest(
                Name: "2025",
                StartDate: new DateOnly(2025, 1, 1),
                EndDate: new DateOnly(2025, 12, 31)));
        var resolver = database.CreateQueryScopeResolver(companyId: 1);

        var result = await resolver.ResolveAsync(
            fiscalYearId: year.Value.Id,
            fromDate: new DateOnly(2026, 1, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "FiscalYears.QueryDateOutsideRange",
            result.Error.Code);
        Assert.Equal("FromDate", result.Error.FieldName);
    }

    private sealed class CapturingAccountingSetupService
        : IDefaultAccountingSetupService
    {
        public List<(int CompanyId, int FiscalYearId)> EnsuredFiscalYears { get; } = [];

        public Task InitializeCompanyAsync(
            int companyId,
            DateOnly effectiveDate,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task EnsureFiscalYearAsync(
            int companyId,
            int fiscalYearId,
            CancellationToken cancellationToken = default)
        {
            EnsuredFiscalYears.Add((companyId, fiscalYearId));
            return Task.CompletedTask;
        }

        public Task EnsureCashboxAsync(
            int companyId,
            int cashboxId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task EnsureCashMovementTypeAsync(
            int companyId,
            int cashMovementTypeId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingInventoryCarryForwardService
        : IFiscalYearInventoryCarryForwardService
    {
        public List<(int SourceId, int TargetId, DateOnly TargetStartDate,
            string SourceName)> Calls { get; } = [];

        public Task<Result> CarryForwardAsync(
            int sourceFiscalYearId,
            int targetFiscalYearId,
            DateOnly targetStartDate,
            string sourceFiscalYearName,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((
                sourceFiscalYearId,
                targetFiscalYearId,
                targetStartDate,
                sourceFiscalYearName));
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class ReadyReadinessService : IAccountingReadinessService
    {
        public Task<Result<AccountingReadinessResponse>> GetAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                    Result<AccountingReadinessResponse>.Success(
                        new AccountingReadinessResponse(
                        FiscalYearId: fiscalYearId,
                        FiscalYearName: "2026",
                        StartDate: new DateOnly(2026, 1, 1),
                        EndDate: new DateOnly(2026, 12, 31),
                        IsReady: true,
                        TotalSources: 0,
                        PostedSources: 0,
                        MissingJournalSources: 0,
                        OrphanAutomaticJournals: 0,
                        DuplicateAutomaticJournals: 0,
                        UnbalancedAutomaticJournals: 0,
                        PendingInventoryCosts: 0,
                        MissingOrInvalidMappings: 0,
                        DeferredPayrollSources: 0,
                        Sources: [],
                        Issues: [])));

        public Task<Result<AccountingBackfillResponse>> BackfillAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FiscalYearTestDatabase : IAsyncDisposable
    {
        private FiscalYearTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        public ApplicationDbContext Context { get; }

        public static async Task<FiscalYearTestDatabase> CreateAsync()
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

            return new FiscalYearTestDatabase(connection, context);
        }

        public FiscalYearService CreateService(
            int companyId,
            IAccountingReadinessService? accountingReadinessService = null,
            IDefaultAccountingSetupService? defaultAccountingSetupService = null,
            IFiscalYearInventoryCarryForwardService?
                inventoryCarryForwardService = null) =>
            new(
                Context,
                new PaginationService(),
                new TestCurrentCompanyContext(companyId),
                TimeProvider.System,
                accountingReadinessService,
                defaultAccountingSetupService,
                inventoryCarryForwardService);

        public IFiscalYearPeriodGuard CreateGuard(int companyId) =>
            new FiscalYearPeriodGuard(
                Context,
                new TestCurrentCompanyContext(companyId));

        public IFiscalYearQueryScopeResolver CreateQueryScopeResolver(
            int companyId) =>
            new FiscalYearQueryScopeResolver(
                Context,
                new TestCurrentCompanyContext(companyId));

        public void ClearTracking() => Context.ChangeTracker.Clear();

        public Task SeedExchangeRateAsync(
            int fiscalYearId,
            DateOnly rateDate,
            decimal rate) =>
            Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO ExchangeRates (
                    CompanyId, FiscalYearId, Currency, RateDate, Rate,
                    Source, LastModifiedAt, RowVersion, CreatedById,
                    CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, {fiscalYearId}, {(int)CurrencyCode.USD}, {rateDate}, {rate},
                    1, {rateDate.ToDateTime(TimeOnly.MinValue)}, randomblob(8),
                    '', {rateDate.ToDateTime(TimeOnly.MinValue)}, '', 0);
                """);

        public Task SeedClosingLedgerAsync(
            int fiscalYearId,
            int nextFiscalYearId,
            JournalPartyType partyType = JournalPartyType.Customer,
            int partyId = 99) =>
            Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Accounts (
                    Id, CompanyId, Code, Name, AccountType, NormalBalance,
                    IsPosting, IsActive, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES
                    (10, 1, '1100', 'Cash', 1, 1, 1, 1, randomblob(8),
                     '', '2026-01-01', '', 0),
                    (20, 1, '4100', 'Revenue', 4, 2, 1, 1, randomblob(8),
                     '', '2026-01-01', '', 0),
                    (30, 1, '3100', 'Opening equity', 3, 2, 1, 1, randomblob(8),
                     '', '2026-01-01', '', 0);

                INSERT INTO AccountMappings (
                    CompanyId, FiscalYearId, MappingType, SourceId, AccountId,
                    RowVersion, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES (
                    1, {nextFiscalYearId}, 17, NULL, 30, randomblob(8),
                    '', '2026-01-01', '', 0);

                INSERT INTO JournalEntries (
                    Id, CompanyId, FiscalYearId, EntryNumber, EntryDate,
                    Description, EntryType, SourceType, SourceId, Status,
                    PostedOn, RowVersion, CreatedById, CreatedOn, CreatedByPc,
                    IsDeleted)
                VALUES (
                    50, 1, {fiscalYearId}, 'JV-1', '2026-12-31', 'ledger',
                    1, NULL, NULL, 1, '2026-12-31', randomblob(8), '',
                    '2026-12-31', '', 0);

                INSERT INTO JournalEntryLines (
                    Id, CompanyId, JournalEntryId, AccountId, PartyType,
                    PartyId, Debit, Credit, Currency, ExchangeRate,
                    TransactionDebit, TransactionCredit, CreatedById,
                    CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (501, 1, 50, 10, {(int)partyType}, {partyId}, 100, 0, 2, 2, 50, 0,
                     '', '2026-12-31', '', 0),
                    (502, 1, 50, 20, NULL, NULL, 0, 100, 1, 1, 0, 100,
                     '', '2026-12-31', '', 0);
                """);

        public Task ChangeClosingAssetBalanceAsync(decimal amount) =>
            Context.Database.ExecuteSqlInterpolatedAsync($"UPDATE JournalEntryLines SET Debit = {amount} WHERE Id = 501");

        public Task SeedEmployeeOperationalBalancesAsync(
            int fiscalYearId,
            int nextFiscalYearId) =>
            Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Accounts (
                    Id, CompanyId, Code, Name, AccountType, NormalBalance,
                    IsPosting, IsActive, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES
                    (40, 1, '1200', 'Employee receivable', 1, 1, 1, 1,
                     randomblob(8), '', '2026-01-01', '', 0),
                    (50, 1, '2100', 'Employee control', 2, 2, 1, 1,
                     randomblob(8), '', '2026-01-01', '', 0);

                INSERT INTO AccountMappings (
                    CompanyId, FiscalYearId, MappingType, SourceId, AccountId,
                    RowVersion, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, {nextFiscalYearId}, 18, NULL, 40, randomblob(8),
                     '', '2026-01-01', '', 0),
                    (1, {nextFiscalYearId}, 11, NULL, 50, randomblob(8),
                     '', '2026-01-01', '', 0);

                INSERT INTO EmployeeOpeningBalances (
                    CompanyId, FiscalYearId, EmployeeId, PayrollEntryId,
                    DocumentNumber, DocumentDate, Currency, ExchangeRate,
                    BalanceType, Amount, BaseAmount, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES (
                    1, {fiscalYearId}, 7, 77, 'PAY-77', '2026-12-01', 1, 1,
                    2, 1000, 1000, '', '2026-12-01', '', 0);

                INSERT INTO EmployeeMovements (
                    CompanyId, FiscalYearId, EmployeeId, CashVoucherId, Type,
                    MovementDate, Currency, Debit, Credit, ExchangeRate,
                    BaseDebit, BaseCredit, CreatedById, CreatedOn, CreatedByPc,
                    IsDeleted)
                VALUES
                    (1, {fiscalYearId}, 7, NULL, 1, '2026-12-10', 1,
                     200, 0, 1, 200, 0, '', '2026-12-10', '', 0),
                    (1, {fiscalYearId}, 7, 700, 1, '2026-12-15', 1,
                     100, 0, 1, 100, 0, '', '2026-12-15', '', 0);

                INSERT INTO JournalEntries (
                    Id, CompanyId, FiscalYearId, EntryNumber, EntryDate,
                    Description, EntryType, SourceType, SourceId, Status,
                    PostedOn, RowVersion, CreatedById, CreatedOn, CreatedByPc,
                    IsDeleted)
                VALUES
                    (60, 1, {fiscalYearId}, 'CV-700', '2026-12-15',
                     'cash voucher', 2, 2, 700, 1, '2026-12-15', randomblob(8),
                     '', '2026-12-15', '', 0),
                    (61, 1, {fiscalYearId}, 'JV-EMP', '2026-12-20',
                     'manual employee line', 1, NULL, NULL, 1, '2026-12-20',
                     randomblob(8), '', '2026-12-20', '', 0);

                INSERT INTO JournalEntryLines (
                    CompanyId, JournalEntryId, AccountId, PartyType, PartyId,
                    Debit, Credit, Currency, ExchangeRate, TransactionDebit,
                    TransactionCredit, CreatedById, CreatedOn, CreatedByPc,
                    IsDeleted)
                VALUES
                    (1, 60, 40, 3, 7, 100, 0, 1, 1, 100, 0,
                     '', '2026-12-15', '', 0),
                    (1, 61, 50, 3, 7, 0, 50, 1, 1, 0, 50,
                     '', '2026-12-20', '', 0);
                """);

        public Task<List<JournalEntryLine>> LoadClosingEmployeeLinesAsync(
            int sourceFiscalYearId) =>
            Context.JournalEntryLines
                .AsNoTracking()
                .Where(line =>
                    line.JournalEntry.CompanyId == 1 &&
                    line.JournalEntry.EntryType == JournalEntryType.Opening &&
                    line.JournalEntry.SourceType ==
                        JournalEntrySourceType.FiscalYearClosing &&
                    line.JournalEntry.SourceId == sourceFiscalYearId &&
                    line.PartyType == JournalPartyType.Employee)
                .OrderBy(line => line.AccountId)
                .ToListAsync();

        public Task<List<ClosingTransferRow>> LoadClosingTransfersAsync(
            int sourceFiscalYearId) =>
            Context.JournalEntries
                .AsNoTracking()
                .Where(entry =>
                    entry.CompanyId == 1 &&
                    entry.EntryType == JournalEntryType.Opening &&
                    entry.SourceType == JournalEntrySourceType.FiscalYearClosing &&
                    entry.SourceId == sourceFiscalYearId)
                .Select(entry => new ClosingTransferRow(
                    entry.FiscalYearId,
                    entry.Lines.Sum(line => line.Debit),
                    entry.Lines.Sum(line => line.Credit)))
                .ToListAsync();

        public Task<JournalEntryLine> LoadClosingPartyLineAsync(
            int sourceFiscalYearId) =>
            Context.JournalEntryLines
                .AsNoTracking()
                .SingleAsync(line =>
                    line.JournalEntry.CompanyId == 1 &&
                    line.JournalEntry.EntryType == JournalEntryType.Opening &&
                    line.JournalEntry.SourceType ==
                        JournalEntrySourceType.FiscalYearClosing &&
                    line.JournalEntry.SourceId == sourceFiscalYearId &&
                    line.AccountId == 10);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static Task CreateSchemaAsync(ApplicationDbContext context) =>
            context.Database.ExecuteSqlRawAsync(
                """
                PRAGMA foreign_keys = ON;

                CREATE TABLE Companies (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NULL
                );

                CREATE TABLE CompanySettings (
                    CompanyId INTEGER PRIMARY KEY,
                    BaseCurrency INTEGER NOT NULL DEFAULT 1
                );

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

                CREATE TABLE FiscalYears (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    IsCurrent INTEGER NOT NULL DEFAULT 0,
                    ClosedOn TEXT NULL,
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
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (CompanyId) REFERENCES Companies (Id)
                );

                CREATE UNIQUE INDEX UX_FiscalYears_Company_Name
                ON FiscalYears (CompanyId, Name)
                WHERE IsDeleted = 0;

                CREATE UNIQUE INDEX UX_FiscalYears_Company_Current
                ON FiscalYears (CompanyId, IsCurrent)
                WHERE IsCurrent = 1 AND IsDeleted = 0;

                CREATE TRIGGER AdvanceFiscalYearRowVersion
                AFTER UPDATE ON FiscalYears
                BEGIN
                    UPDATE FiscalYears
                    SET RowVersion = randomblob(8)
                    WHERE Id = NEW.Id;
                END;

                CREATE TABLE Accounts (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    ParentAccountId INTEGER NULL,
                    AccountType INTEGER NOT NULL,
                    NormalBalance INTEGER NOT NULL,
                    IsPosting INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL,
                    RowVersion BLOB NOT NULL,
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

                CREATE TABLE AccountMappings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    MappingType INTEGER NOT NULL,
                    SourceId INTEGER NULL,
                    AccountId INTEGER NOT NULL,
                    RowVersion BLOB NOT NULL,
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

                CREATE TABLE JournalEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    EntryNumber TEXT NOT NULL,
                    EntryDate TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    EntryType INTEGER NOT NULL,
                    SourceType INTEGER NULL,
                    SourceId INTEGER NULL,
                    SourceNumber TEXT NULL,
                    Status INTEGER NOT NULL,
                    PostedOn TEXT NOT NULL,
                    ReversedOn TEXT NULL,
                    ReversalOfEntryId INTEGER NULL,
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

                CREATE TABLE JournalEntryLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    JournalEntryId INTEGER NOT NULL,
                    AccountId INTEGER NOT NULL,
                    PartyType INTEGER NULL,
                    PartyId INTEGER NULL,
                    Description TEXT NULL,
                    Debit NUMERIC NOT NULL,
                    Credit NUMERIC NOT NULL,
                    Currency INTEGER NOT NULL DEFAULT 1,
                    ExchangeRate NUMERIC NOT NULL DEFAULT 1,
                    TransactionDebit NUMERIC NOT NULL DEFAULT 0,
                    TransactionCredit NUMERIC NOT NULL DEFAULT 0,
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

                CREATE TABLE EmployeeOpeningBalances (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    EmployeeId INTEGER NOT NULL,
                    PayrollEntryId INTEGER NULL,
                    DocumentNumber TEXT NOT NULL,
                    DocumentDate TEXT NOT NULL,
                    Currency INTEGER NOT NULL,
                    ExchangeRateId INTEGER NULL,
                    ExchangeRate NUMERIC NOT NULL DEFAULT 1,
                    BalanceType INTEGER NOT NULL,
                    Amount NUMERIC NOT NULL,
                    BaseAmount NUMERIC NOT NULL DEFAULT 0,
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
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE EmployeeMovements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    EmployeeId INTEGER NOT NULL,
                    CashVoucherId INTEGER NULL,
                    Type INTEGER NOT NULL,
                    MovementDate TEXT NOT NULL,
                    Currency INTEGER NOT NULL,
                    Debit NUMERIC NOT NULL DEFAULT 0,
                    Credit NUMERIC NOT NULL DEFAULT 0,
                    ExchangeRate NUMERIC NOT NULL DEFAULT 1,
                    BaseDebit NUMERIC NOT NULL DEFAULT 0,
                    BaseCredit NUMERIC NOT NULL DEFAULT 0,
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
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                INSERT INTO Companies (Id, Name)
                VALUES (1, 'Company 1'), (2, 'Company 2');

                INSERT INTO CompanySettings (CompanyId, BaseCurrency)
                VALUES (1, 1), (2, 1);
                """);

        private sealed record TestCurrentCompanyContext(int CompanyId)
            : ICurrentCompanyContext;

        public sealed record ClosingTransferRow(
            int FiscalYearId,
            decimal Debit,
            decimal Credit);
    }
}
