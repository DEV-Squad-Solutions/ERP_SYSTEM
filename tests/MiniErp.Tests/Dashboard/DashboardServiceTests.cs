using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.Dashboard;
using MiniErp.Application.Features.ProfitabilityReports;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Dashboard;

namespace MiniErp.Tests.Dashboard;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task Get_UsesCurrentFiscalYearAndReturnsZeroMonths()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.FiscalYearId);
        Assert.Equal(new DateOnly(2026, 1, 1), result.Value.FromDate);
        Assert.Equal(new DateOnly(2026, 12, 31), result.Value.ToDate);
        Assert.Equal(CurrencyCode.EGP, result.Value.BaseCurrency);
        Assert.Equal(12, result.Value.MonthlyActivity.Count);
        Assert.All(result.Value.MonthlyActivity, month =>
        {
            Assert.Equal(0m, month.Sales);
            Assert.Equal(0m, month.Purchases);
        });
        Assert.True(result.Value.Accounting.IsReady);
        Assert.Empty(result.Value.Alerts);
    }

    [Fact]
    public async Task Get_RejectsRangeOutsideOneFiscalYear()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest(
                FromDate: new DateOnly(2025, 12, 31),
                ToDate: new DateOnly(2026, 1, 2)));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error =>
            error.Code == "Dashboard.FiscalYearNotFound");
    }

    [Fact]
    public async Task Get_CalculatesBaseCurrencyTotalsReturnsAndOutstanding()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.SeedInvoicesAsync();

        var result = await database.Service.GetAsync(
            new DashboardFilterRequest(
                FromDate: new DateOnly(2026, 9, 1),
                ToDate: new DateOnly(2026, 9, 30)));

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.Sales.Total);
        Assert.Equal(10m, result.Value.Sales.Returns);
        Assert.Equal(90m, result.Value.Sales.Net);
        Assert.Equal(80m, result.Value.Sales.Outstanding);
        Assert.Equal(50m, result.Value.Purchases.Total);
        Assert.Equal(5m, result.Value.Purchases.Returns);
        Assert.Equal(45m, result.Value.Purchases.Net);
        Assert.Equal(40m, result.Value.Purchases.Outstanding);
        Assert.Equal(4, result.Value.Counts.InvoiceCount);
        Assert.Equal(2, result.Value.Counts.BusinessPartnerCount);
        Assert.Equal(2, result.Value.InvoiceStatus.PartiallyPaidCount);
        Assert.Equal(2, result.Value.InvoiceStatus.OverdueCount);
        Assert.Equal(120m, result.Value.InvoiceStatus.OverdueAmount);
        Assert.Equal(90m, result.Value.MonthlyActivity.Single().Sales);
        Assert.Equal(45m, result.Value.MonthlyActivity.Single().Purchases);
        Assert.Contains(result.Value.Alerts, alert =>
            alert.Code == "OverdueInvoices" && alert.Count == 2);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context,
            DashboardService service)
        {
            this.connection = connection;
            Context = context;
            Service = service;
        }

        public ApplicationDbContext Context { get; }

        public DashboardService Service { get; }

        public async Task SeedInvoicesAsync()
        {
            var auditDate = new DateTime(2026, 9, 1);
            var partner = new BusinessPartner
            {
                CompanyId = 1,
                Code = "BP-1",
                Name = "Dashboard Partner",
                Currency = CurrencyCode.EGP,
                IsActive = true,
                CreatedById = "test",
                CreatedByPc = "test",
                CreatedOn = auditDate
            };
            var inactivePartner = new BusinessPartner
            {
                CompanyId = 1,
                Code = "BP-2",
                Name = "Inactive Dashboard Partner",
                Currency = CurrencyCode.EGP,
                IsActive = false,
                CreatedById = "test",
                CreatedByPc = "test",
                CreatedOn = auditDate
            };
            var store = new Store
            {
                CompanyId = 1,
                Code = "ST-1",
                Name = "Dashboard Store",
                IsActive = true,
                CreatedById = "test",
                CreatedByPc = "test",
                CreatedOn = auditDate
            };
            Context.AddRange(partner, inactivePartner, store);
            await Context.SaveChangesAsync();

            await Context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Invoices (
                    Id, CompanyId, InvoiceNumber, InvoiceType, ContentType,
                    PaymentTerm, InvoiceDate, DueDate, BusinessPartnerId,
                    StoreId, Currency, ExchangeRate, UsesExternalDriver,
                    DiscountAmount, WBWeight, WBScaleDifference, WBDiscount,
                    WBTotal, PaidAmount, Total, BaseSubtotal,
                    BaseDiscountAmount, BaseTotal,
                    BasePaidAmountAtInvoiceRate, LastModifiedAt, RowVersion,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 'S-1', 1, 1, 1, '2026-09-03', '2026-09-03',
                     1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 20, 100, 100, 0, 100,
                     20, '2026-09-03', randomblob(8), 'test', '2026-09-03',
                     'test', 0),
                    (2, 1, 'SR-1', 3, 1, 1, '2026-09-03', NULL,
                     1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 10, 10, 0, 10,
                     0, '2026-09-03', randomblob(8), 'test', '2026-09-03',
                     'test', 0),
                    (3, 1, 'P-1', 2, 1, 1, '2026-09-03', '2026-09-03',
                     1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 10, 50, 50, 0, 50,
                     10, '2026-09-03', randomblob(8), 'test', '2026-09-03',
                     'test', 0),
                    (4, 1, 'PR-1', 4, 1, 1, '2026-09-03', NULL,
                     1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 5, 5, 0, 5,
                     0, '2026-09-03', randomblob(8), 'test', '2026-09-03',
                     'test', 0);
                """);
            Context.ChangeTracker.Clear();
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Companies (
                    Id, Name, Address, CommercialRegister, TaxNumber,
                    ManagerName, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES (
                    1, 'Dashboard Test Company', 'Test Address',
                    'CR-DASHBOARD', 'TAX-DASHBOARD', 'Test Manager',
                    randomblob(8), 'test', '2026-01-01', 'test', 0);

                INSERT INTO CompanySettings (
                    CompanyId, BaseCurrency, StockBalanceCheckMode)
                VALUES (1, 1, 1);

                INSERT INTO FiscalYears (
                    Id, CompanyId, Name, StartDate, EndDate, Status,
                    IsCurrent, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                VALUES (
                    1, 1, '2026', '2026-01-01', '2026-12-31', 1, 1,
                    randomblob(8), 'test', '2026-01-01', 'test', 0);
                """);

            var service = new DashboardService(
                context,
                new TestCurrentCompanyContext(1),
                new EmptyProfitabilityService(),
                new ReadyAccountingService(),
                new FixedTimeProvider(
                    new DateTimeOffset(
                        2026,
                        9,
                        4,
                        12,
                        0,
                        0,
                        TimeSpan.Zero)));
            return new TestDatabase(connection, context, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class EmptyProfitabilityService
        : IProfitabilityReportService
    {
        public Task<Result<InvoiceProfitabilityListResponse>> GetInvoicesAsync(
            PaginationRequest pagination,
            ProfitabilityReportFilterRequest filters,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<InvoiceProfitabilityListResponse>.Success(
                new InvoiceProfitabilityListResponse(
                    IncludeReturns: true,
                    BaseCurrency: CurrencyCode.EGP,
                    FromDate: filters.FromDate,
                    ToDate: filters.ToDate,
                    Invoices: [],
                    PageNumber: pagination.PageNumber,
                    PageSize: pagination.PageSize,
                    TotalCount: 0,
                    TotalPages: 0,
                    Summary: new ProfitabilityReportSummaryResponse(
                        SalesRevenue: 0m,
                        SalesCost: 0m,
                        ReturnRevenue: 0m,
                        ReturnCost: 0m,
                        NetRevenue: 0m,
                        RecognizedCost: 0m,
                        GrossProfit: 0m,
                        GrossMarginPercentage: null,
                        FinalizedNetRevenue: 0m,
                        FinalizedCost: 0m,
                        FinalizedGrossProfit: 0m,
                        FinalizedGrossMarginPercentage: null,
                        PendingRevenue: 0m,
                        PendingCostQuantity: 0m,
                        InvoiceCount: 0,
                        ItemCount: 0,
                        LineCount: 0,
                        PendingInvoiceCount: 0,
                        PendingLineCount: 0))));

        public Task<Result<InvoiceProfitabilityResponse>>
            GetInvoiceDetailsAsync(
                int invoiceId,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<ItemProfitabilityListResponse>> GetItemsAsync(
            PaginationRequest pagination,
            ProfitabilityReportFilterRequest filters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ReadyAccountingService
        : IAccountingReadinessService
    {
        public Task<Result<AccountingReadinessResponse>> GetAsync(
            int fiscalYearId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AccountingReadinessResponse>.Success(
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
}
