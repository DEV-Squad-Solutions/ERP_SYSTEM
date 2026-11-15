using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using MiniErp.Api.Controllers;
using MiniErp.Application.Common.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Services.AccountMappings;
using MiniErp.Infrastructure.Services.DriverTrips;
using MiniErp.Infrastructure.Services.FiscalYears;
using MiniErp.Infrastructure.Services.JournalEntries;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Tests.TestDoubles;

namespace MiniErp.Tests.CashManagement;

public sealed class StandaloneDriverTripServiceTests
{
    static StandaloneDriverTripServiceTests() => MappingConfiguration.Register(typeof(InfrastructureAssemblyMarker).Assembly);

    [Fact]
    public async Task StandaloneCreatePostsPreciseCostAndAppearsInStatementWithoutInvoiceOrParty()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await ConfigureAsync(db);
        var result = await CreateAsync(Service(db));
        Assert.True(result.IsSuccess, result.Error.Description);
        var row = result.Value;
        Assert.Null(row.InvoiceId);
        Assert.Null(row.InvoiceNumber);
        Assert.Null(row.BusinessPartnerId);
        Assert.Null(row.BusinessPartnerName);
        Assert.Null(row.CountryName);
        Assert.Equal(1, row.DriverId);
        Assert.Equal(1, row.FiscalYearId);
        Assert.Equal(123.45m, row.Cost);
        Assert.Equal("123.45", row.CostText);
        Assert.True(row.IsStandalone);
        Assert.False(row.IsReadOnly);
        Assert.Equal("Actual driver", row.ActualDriverName);
        Assert.Equal(CurrencyCode.EGP, row.BaseCurrency);
        Assert.Equal("Road", row.CostNotes);
        Assert.Equal(8, row.RowVersion.Length);
        var entry = await db.Context.JournalEntries.Include(x => x.Lines)
            .SingleAsync(x => x.SourceType == JournalEntrySourceType.DriverTrip && x.SourceId == row.DriverTripId);
        Assert.Equal($"TR-{row.DriverTripId}", entry.SourceNumber);
        var expense = Assert.Single(entry.Lines, x => x.AccountId == 102);
        Assert.Equal(123.45m, expense.Debit);
        Assert.Equal(0m, expense.Credit);
        var driver = Assert.Single(entry.Lines, x => x.AccountId == 101);
        Assert.Equal(0m, driver.Debit);
        Assert.Equal(123.45m, driver.Credit);
        Assert.Equal(JournalPartyType.Driver, driver.PartyType);
        Assert.Equal(1, driver.PartyId);
        var statement = await db.CreateStatementService(1).GetDriverStatementAsync(Page(),
            new DriverStatementFilterRequest(DriverId: 1, FiscalYearId: 1));
        Assert.True(statement.IsSuccess, statement.Error.Description);
        var movement = Assert.Single(statement.Value.Items, x => x.DriverTripId == row.DriverTripId);
        Assert.Null(movement.InvoiceNumber);
        Assert.Null(movement.BusinessPartnerId);
        Assert.Null(movement.BusinessPartnerName);
    }

    [Fact]
    public async Task StandaloneUpdateReplacesJournalAndZeroRemovesItWhileVersionsProtectWrites()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await ConfigureAsync(db);
        var service = Service(db);
        var first = (await CreateAsync(service)).Value;
        var updated = await UpdateAsync(service, first, cost: 40.12m, driverId: 2, partnerId: 1);
        Assert.True(updated.IsSuccess, updated.Error.Description);
        Assert.Equal(2, updated.Value.DriverId);
        Assert.Equal(1, updated.Value.BusinessPartnerId);
        Assert.Equal(40.12m, updated.Value.Cost);
        Assert.NotEqual(first.RowVersion, updated.Value.RowVersion);
        var entry = await db.Context.JournalEntries.AsNoTracking().Include(x => x.Lines)
            .SingleAsync(x => x.SourceType == JournalEntrySourceType.DriverTrip && x.SourceId == first.DriverTripId);
        var line = Assert.Single(entry.Lines, x => x.PartyType == JournalPartyType.Driver);
        Assert.Equal(2, line.PartyId);
        Assert.Equal(40.12m, line.Credit);
        Assert.Equal("DriverTrips.Concurrency", (await UpdateAsync(service, first, 9m)).Error.Code);
        var zero = await UpdateAsync(service, updated.Value, 0m, driverId: 2, partnerId: 1);
        Assert.True(zero.IsSuccess, zero.Error.Description);
        Assert.False(await db.Context.JournalEntries.AnyAsync(x => x.SourceType == JournalEntrySourceType.DriverTrip && x.SourceId == first.DriverTripId));
        Assert.Equal("DriverTrips.Concurrency", (await DeleteAsync(service, first.DriverTripId, first.RowVersion)).Error.Code);
        Assert.True((await DeleteAsync(service, zero.Value.DriverTripId, zero.Value.RowVersion)).IsSuccess);
        Assert.False(await db.Context.DriverTrips.AnyAsync(x => x.Id == first.DriverTripId));
    }

    [Fact]
    public async Task CompanyYearDriverPartyAndAmountBoundariesRejectBeforeWriting()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await ConfigureAsync(db);
        var service = Service(db);
        foreach (var input in new[] {
            Body(driverId: 3), Body(partnerId: 3), Body(fiscalYearId: 2),
            Body(cost: -1m), Body(cost: 0.001m), Body(cost: 1000000000000000m),
            Body(date: new DateOnly(2040, 1, 1)), Body(partnerId: 4) })
        {
            var rejected = await service.AddAsync(input);
            Assert.True(rejected.IsFailure);
        }
        Assert.Equal(3, await db.Context.DriverTrips.CountAsync());
        await db.Context.Database.ExecuteSqlRawAsync("UPDATE FiscalYears SET Status = 2 WHERE Id = 1");
        Assert.True((await CreateAsync(service)).IsFailure);
        Assert.Equal(3, await db.Context.DriverTrips.CountAsync());
    }

    [Fact]
    public async Task MissingPostingConfigurationRollsBackSourceAndInvoiceOwnedTripCannotBeEdited()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        var service = Service(db);
        var rejected = await CreateAsync(service);
        Assert.True(rejected.IsFailure);
        Assert.Equal(3, await db.Context.DriverTrips.CountAsync());
        var invoice = Assert.Single((await service.GetCostEntryAsync(Page(), new DriverTripCostFilterRequest(TripNumber: "TR-1"))).Value.Items);
        Assert.Equal("DriverTrips.InvoiceOwned", (await UpdateAsync(service, invoice, 10m)).Error.Code);
        Assert.Equal("DriverTrips.InvoiceOwned", (await DeleteAsync(service, invoice.DriverTripId, invoice.RowVersion)).Error.Code);
    }

    [Fact]
    public async Task PostedVouchersProtectTripIdentityAndDeletionWhileCostRemainsEditable()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await ConfigureAsync(db);
        var service = Service(db);
        var trip = (await service.AddAsync(Body())).Value;
        var vouchers = db.CreateVoucherService(1);
        var draft = (await vouchers.AddAsync(new CashVoucherRequest(
            VoucherDate: trip.TripDate, Direction: CashDirection.Payment, CashboxId: 1, Amount: 5m, Description: "Driver payment"))).Value;
        var completed = await vouchers.UpdateAsync(draft.Id, new CashVoucherUpdateRequest(
            VoucherDate: trip.TripDate, Direction: CashDirection.Payment, CashboxId: 1, CashMovementTypeId: null,
            EmployeeId: null, BusinessPartnerId: null, DriverId: 1, DriverTripId: trip.DriverTripId,
            ExternalPartyName: null, Amount: 5m, ReferenceNumber: null, Description: "Driver payment", Notes: null, RowVersion: draft.RowVersion));
        Assert.True(completed.IsSuccess, completed.Error.Description);
        Assert.Equal("DriverTrips.LinkedVoucher", (await UpdateAsync(service, trip, 10m, driverId: 2)).Error.Code);
        Assert.Equal("DriverTrips.LinkedVoucher", (await service.DeleteAsync(trip.DriverTripId, trip.RowVersion)).Error.Code);
        Assert.Equal("CashVouchers.DeleteNotAllowed", (await vouchers.DeleteAsync(draft.Id)).Error.Code);
        Assert.True(await db.Context.CashVouchers.AnyAsync(voucher => voucher.Id == draft.Id));
        Assert.Equal("DriverTrips.LinkedVoucher", (await service.DeleteAsync(trip.DriverTripId, trip.RowVersion)).Error.Code);
        Assert.Equal("DriverTrips.LinkedVoucher", (await UpdateAsync(service, trip, 10m, partnerId: 1)).Error.Code);
        Assert.True((await UpdateAsync(service, trip, 7m)).IsSuccess);
    }

    [Fact]
    public async Task LegacyDeletedVoucherStillProtectsTripIdentityAndDeletion()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await ConfigureAsync(db);
        var service = Service(db);
        var trip = (await service.AddAsync(Body())).Value;
        // Seed history that predates the standalone delete prohibition. The
        // application cannot create this state through its delete endpoint.
        var history = new CashVoucher
        {
            CompanyId = 1, VoucherNumber = "LEGACY-DELETED-DRIVER-VOUCHER",
            VoucherDate = trip.TripDate, Direction = CashDirection.Payment,
            CashboxId = 1, PartyType = CashPartyType.Driver,
            DriverId = 1, DriverTripId = trip.DriverTripId,
            Amount = 5m, Currency = CurrencyCode.EGP,
            IsPosted = true, IsDeleted = true
        };
        history.ApplyExchangeRate(exchangeRateId: null, exchangeRate: 1m);
        db.Context.CashVouchers.Add(history);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        Assert.False(await db.Context.CashVouchers.AnyAsync(voucher => voucher.Id == history.Id));
        Assert.True(await db.Context.CashVouchers.IgnoreQueryFilters().AnyAsync(voucher =>
            voucher.Id == history.Id && voucher.IsDeleted));
        Assert.Equal("DriverTrips.LinkedVoucher", (await UpdateAsync(service, trip, 10m, driverId: 2)).Error.Code);
        Assert.Equal("DriverTrips.LinkedVoucher", (await UpdateAsync(service, trip, 10m, partnerId: 1)).Error.Code);
        Assert.Equal("DriverTrips.LinkedVoucher", (await service.DeleteAsync(trip.DriverTripId, trip.RowVersion)).Error.Code);
        Assert.True((await UpdateAsync(service, trip, 7m)).IsSuccess);
    }

    [Fact]
    public async Task VoucherCompletionRejectsTripFromAnotherFiscalYear()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await db.ConfigureSeparateFiscalYearsAsync();
        var vouchers = db.CreateVoucherService(1);
        var draft = (await vouchers.AddAsync(new CashVoucherRequest(
            VoucherDate: new DateOnly(2026, 7, 25), Direction: CashDirection.Payment, CashboxId: 1, Amount: 5m, Description: null))).Value;
        var result = await vouchers.UpdateAsync(draft.Id, new CashVoucherUpdateRequest(
            VoucherDate: draft.VoucherDate, Direction: CashDirection.Payment, CashboxId: 1, CashMovementTypeId: null,
            EmployeeId: null, BusinessPartnerId: null, DriverId: 1, DriverTripId: 1, ExternalPartyName: null, Amount: 5m,
            ReferenceNumber: null, Description: null, Notes: null, RowVersion: draft.RowVersion));
        Assert.True(result.IsFailure);
        Assert.False((await db.Context.CashVouchers.AsNoTracking().SingleAsync(voucher => voucher.Id == draft.Id)).IsPosted);
    }

    [Fact]
    public async Task DetailFiltersAndClosedYearWritesRespectSelectedCompanyAndYear()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await ConfigureAsync(db);
        var service = Service(db);
        var row = (await service.AddAsync(Body(cost: null))).Value;
        var standalone = await service.GetCostEntryAsync(Page(), new DriverTripCostFilterRequest(IsStandalone: true, FiscalYearId: 1));
        Assert.Equal(row.DriverTripId, Assert.Single(standalone.Value.Items).DriverTripId);
        Assert.All((await service.GetCostEntryAsync(Page(), new DriverTripCostFilterRequest(IsStandalone: false))).Value.Items,
            item => Assert.False(item.IsStandalone));
        Assert.Equal("DriverTrips.NotFound", (await service.GetByIdAsync(3, 1)).Error.Code);
        Assert.True((await service.GetByIdAsync(row.DriverTripId, 2)).IsFailure);
        var wrongYear = new DriverTripUpdateRequest(DriverId: 1, FiscalYearId: 2, TripDate: row.TripDate,
            BusinessPartnerId: null, ActualDriverName: null, Cost: null, Notes: null, RowVersion: row.RowVersion);
        Assert.Equal("DriverTrips.FiscalYearImmutable", (await service.UpdateAsync(row.DriverTripId, wrongYear)).Error.Code);
        await db.Context.Database.ExecuteSqlRawAsync("UPDATE FiscalYears SET Status=2 WHERE Id=1");
        Assert.True((await service.GetByIdAsync(row.DriverTripId, 1)).Value.IsReadOnly);
        Assert.True((await UpdateAsync(service, row, 2m)).IsFailure);
        Assert.True((await service.DeleteAsync(row.DriverTripId, row.RowVersion)).IsFailure);
        Assert.Equal("DriverTrips.RowVersionRequired", (await service.DeleteAsync(row.DriverTripId, null)).Error.Code);
    }

    [Fact]
    public void ExactCostStringsDeserializeAndValidatorsRejectInvalidAmounts()
    {
        const string json = """{"DriverId":1,"FiscalYearId":1,"TripDate":"2026-07-25","Cost":"999999999999999.99"}""";
        var request = JsonSerializer.Deserialize<DriverTripRequest>(json)!;
        Assert.Equal(999999999999999.99m, request.Cost);
        Assert.True(new DriverTripRequestValidator().Validate(request).IsValid);
        Assert.False(new DriverTripRequestValidator().Validate(request with { Cost = 1.001m }).IsValid);
        var bulk = JsonSerializer.Deserialize<DriverTripCostUpdateItem>("""{"DriverTripId":1,"Cost":"123456789012345.67","Notes":null,"RowVersion":"AAAAAAAAAAA="}""")!;
        Assert.Equal(123456789012345.67m, bulk.Cost);
    }

    [Fact]
    public async Task ExistingInactiveDriverCanRetainTripWhileNewDocumentsRequireActiveDriver()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await ConfigureAsync(db);
        var service = Service(db);
        var trip = (await CreateAsync(service)).Value;
        await db.Context.Database.ExecuteSqlRawAsync("UPDATE Drivers SET IsActive=0 WHERE Id=1");
        Assert.Equal("DriverTrips.DriverUnavailable", (await CreateAsync(service)).Error.Code);
        var updated = await UpdateAsync(service, trip, 88m);
        Assert.True(updated.IsSuccess, updated.Error.Description);
        Assert.Equal(88m, updated.Value.Cost);
    }

    [Fact]
    public async Task BulkCostEntrySupportsExplicitOpenNoncurrentYear()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        await db.ConfigureSeparateFiscalYearsAsync();
        await db.Context.Database.ExecuteSqlRawAsync("UPDATE FiscalYears SET Status=1 WHERE Id=1");
        var service = db.CreateDriverTripService(1);
        var row = (await service.GetByIdAsync(1, 1)).Value;
        var updated = await service.UpdateCostsAsync(new DriverTripBulkCostUpdateRequest([
            new DriverTripCostUpdateItem(DriverTripId: row.DriverTripId, Cost: 12m, Notes: null, RowVersion: row.RowVersion)]));
        Assert.True(updated.IsSuccess, updated.Error.Description);
        Assert.Equal(1, Assert.Single(updated.Value.Items).FiscalYearId);
    }

    [Theory]
    [InlineData(nameof(DriverTripsController.Create))]
    [InlineData(nameof(DriverTripsController.Update))]
    [InlineData(nameof(DriverTripsController.Delete))]
    [InlineData(nameof(DriverTripsController.UpdateCosts))]
    public void EveryTripWriteRequiresAdmin(string action)
    {
        var attributes = typeof(DriverTripsController).GetMethod(action)!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>();
        Assert.Contains(attributes, attribute => attribute.Roles == ApplicationRoles.Admin);
    }

    [Fact]
    public async Task BulkNullItemsAndMissingPostingServiceReturnValidationOrConfigurationFailures()
    {
        await using var db = await CashManagementTestDatabase.CreateAsync();
        Assert.True((await Service(db).UpdateCostsAsync(new DriverTripBulkCostUpdateRequest([null!]))).IsFailure);
        Assert.False(new DriverTripBulkCostUpdateRequestValidator().Validate(new DriverTripBulkCostUpdateRequest(null!)).IsValid);
        var company = new TestCurrentCompanyContext(1);
        var service = new DriverTripService(db.Context, new PaginationService(), company,
            new FiscalYearQueryScopeResolver(db.Context, company), new FiscalYearPeriodGuard(db.Context, company));
        Assert.Equal("DriverTrips.PostingUnavailable", (await service.AddAsync(Body())).Error.Code);
    }

    private static DriverTripRequest Body(int driverId = 1, int fiscalYearId = 1, int? partnerId = null, decimal? cost = 123.45m, DateOnly? date = null) =>
        new(DriverId: driverId, FiscalYearId: fiscalYearId, TripDate: date ?? new DateOnly(2026, 7, 25), BusinessPartnerId: partnerId,
            ActualDriverName: "Actual driver", Cost: cost, Notes: "Road");

    private static Task<Result<DriverTripCostResponse>> CreateAsync(DriverTripService service) => service.AddAsync(Body());
    private static Task<Result<DriverTripCostResponse>> UpdateAsync(DriverTripService service, DriverTripCostResponse row, decimal cost, int driverId = 1, int? partnerId = null) =>
        service.UpdateAsync(row.DriverTripId, new DriverTripUpdateRequest(DriverId: driverId, FiscalYearId: row.FiscalYearId,
            TripDate: row.TripDate, BusinessPartnerId: partnerId, ActualDriverName: row.ActualDriverName, Cost: cost, Notes: "Road", RowVersion: row.RowVersion));

    private static Task<Result> DeleteAsync(DriverTripService service, int id, byte[] version) => service.DeleteAsync(id, version);

    private static DriverTripService Service(CashManagementTestDatabase db)
    {
        var company = new TestCurrentCompanyContext(1);
        return new DriverTripService(db.Context, new PaginationService(), company,
            new FiscalYearQueryScopeResolver(db.Context, company), new FiscalYearPeriodGuard(db.Context, company),
            new DriverTripPostingService(db.Context, company, new AccountMappingResolver(db.Context, company),
                new AutomaticPostingService(db.Context, company, TimeProvider.System, NullLogger<AutomaticPostingService>.Instance)));
    }

    private static Task ConfigureAsync(CashManagementTestDatabase db) => db.Context.Database.ExecuteSqlRawAsync("""
        INSERT INTO Accounts (Id, CompanyId, Code, Name, ParentAccountId, AccountType, NormalBalance, IsPosting, IsActive, IsDeleted)
        VALUES (101, 1, '2100', 'Driver Control', 50, 2, 2, 1, 1, 0), (102, 1, '5100', 'Trip Expense', 50, 5, 1, 1, 1, 0);
        INSERT INTO AccountMappings (CompanyId,FiscalYearId,MappingType,SourceId,AccountId,CreatedById,CreatedOn,CreatedByPc,IsDeleted)
        VALUES (1,1,12,NULL,101,'test','2026-01-01','test',0), (1,1,19,NULL,102,'test','2026-01-01','test',0);
        """);

    private static PaginationRequest Page() => new() { PageNumber = 1, PageSize = 100 };
    private sealed record TestCurrentCompanyContext(int CompanyId) : ICurrentCompanyContext;
}
