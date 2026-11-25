using System.Data;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.Companies;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.AccountingReadiness.AccountingReadinessErrors;

namespace MiniErp.Infrastructure.Services.AccountingReadiness;

public sealed partial class AccountingReadinessService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IInventoryCostingService inventoryCostingService,
    IInvoicePostingService invoicePostingService,
    ICashVoucherPostingService cashVoucherPostingService,
    ICashboxTransferPostingService cashboxTransferPostingService,
    IInventoryPostingService inventoryPostingService,
    IOpeningBalancePostingService openingBalancePostingService,
    IDriverTripPostingService driverTripPostingService,
    ILogger<AccountingReadinessService> logger,
    IDefaultAccountingSetupService? defaultAccountingSetupService = null)
    : IAccountingReadinessService
{
    private const string MeterName = "MiniErp.Accounting";
    private static readonly Meter AccountingMeter = new(MeterName);
    private static readonly Counter<long> ReadinessChecks =
        AccountingMeter.CreateCounter<long>(
            "mini_erp.accounting.readiness.checks",
            unit: "{check}",
            description: "Accounting readiness check outcomes.");
    private static readonly Counter<long> Backfills =
        AccountingMeter.CreateCounter<long>(
            "mini_erp.accounting.readiness.backfills",
            unit: "{backfill}",
            description: "Accounting backfill outcomes.");
    private static readonly Counter<long> ReadinessFailures =
        AccountingMeter.CreateCounter<long>(
            "mini_erp.accounting.readiness.failures",
            unit: "{failure}",
            description: "Accounting readiness and backfill failures.");
    private readonly int companyId = currentCompanyContext.CompanyId;

    private static class LogEvents
    {
        public static readonly EventId ReadinessStarted =
            new(5201, "AccountingReadinessStarted");
        public static readonly EventId ReadinessCompleted =
            new(5202, "AccountingReadinessCompleted");
        public static readonly EventId ReadinessFailed =
            new(5203, "AccountingReadinessFailed");
        public static readonly EventId BackfillStarted =
            new(5211, "AccountingBackfillStarted");
        public static readonly EventId BackfillCompleted =
            new(5212, "AccountingBackfillCompleted");
        public static readonly EventId BackfillFailed =
            new(5213, "AccountingBackfillFailed");
    }

    private sealed class MissingOpeningLine
    {
        public int OpeningBalanceId { get; init; }
        public int StoreId { get; init; }
        public int ItemId { get; init; }
        public int? ItemUnitId { get; init; }
        public decimal Quantity { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public DateOnly DocumentDate { get; init; }
    }
}
