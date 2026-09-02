using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.AccountMappings;
using MiniErp.Infrastructure.Services.CashboxTransfers;
using MiniErp.Infrastructure.Services.CashVouchers;
using MiniErp.Infrastructure.Services.DriverTrips;
using MiniErp.Infrastructure.Services.Inventory;
using MiniErp.Infrastructure.Services.Invoices;
using MiniErp.Infrastructure.Services.JournalEntries;

namespace MiniErp.Tests.Accounting;

public sealed class AutomaticPostingServiceTests
{
    [Fact]
    public async Task Posting_IsIdempotent_AndCorrectionUpdatesSameEntry()
    {
        await using var database = await TestDatabase.CreateAsync();
        var logger = new RecordingLogger<AutomaticPostingService>();
        var service = new AutomaticPostingService(
            database.Context,
            new TestCurrentCompanyContext(1),
            TimeProvider.System,
            logger);
        var request = CreateRequest(amount: 100m);

        var created = await service.CreateOrGetAsync(request);
        var duplicate = await service.CreateOrGetAsync(request);
        var updated = await service.CreateOrUpdateAsync(
            CreateRequest(amount: 125m));

        Assert.True(created.IsSuccess);
        Assert.True(created.Value.Created);
        Assert.True(duplicate.IsSuccess);
        Assert.False(duplicate.Value.Created);
        Assert.Equal(created.Value.JournalEntryId, duplicate.Value.JournalEntryId);
        Assert.True(updated.IsSuccess);
        Assert.False(updated.Value.Created);
        Assert.Equal(created.Value.JournalEntryId, updated.Value.JournalEntryId);

        var entries = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .OrderBy(entry => entry.Id)
            .ToListAsync();
        var entry = Assert.Single(entries);
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.Null(entry.ReversalOfEntryId);
        Assert.Equal(125m, entry.Lines.Sum(line => line.Debit));
        Assert.Equal(125m, entry.Lines.Sum(line => line.Credit));
        Assert.Contains(logger.Entries, entry =>
            entry.EventId.Name == "AutomaticJournalCreated");
        Assert.Contains(logger.Entries, entry =>
            entry.EventId.Name == "AutomaticPostingIdempotent");
        Assert.Contains(logger.Entries, entry =>
            entry.EventId.Name == "AutomaticJournalUpdated");
    }

    [Fact]
    public async Task Delete_IsIdempotent_AndRemovesSourceEntry()
    {
        await using var database = await TestDatabase.CreateAsync();
        var logger = new RecordingLogger<AutomaticPostingService>();
        var service = new AutomaticPostingService(
            database.Context,
            new TestCurrentCompanyContext(1),
            TimeProvider.System,
            logger);
        var created = await service.CreateOrGetAsync(CreateRequest(100m));

        var deleted = await service.DeleteAsync(
            JournalEntrySourceType.CashVoucher,
            sourceId: 42);
        var repeated = await service.DeleteAsync(
            JournalEntrySourceType.CashVoucher,
            sourceId: 42);

        Assert.True(created.IsSuccess);
        Assert.True(deleted.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.False(await database.Context.JournalEntries
            .AsNoTracking()
            .AnyAsync(entry =>
                entry.SourceType == JournalEntrySourceType.CashVoucher &&
                entry.SourceId == 42 &&
                entry.Status == JournalEntryStatus.Posted &&
                entry.ReversalOfEntryId == null));
        Assert.Empty(await database.Context.JournalEntries
            .AsNoTracking()
            .ToListAsync());
        Assert.Contains(logger.Entries, entry =>
            entry.EventId.Name == "AutomaticJournalDeleted");
        Assert.Contains(logger.Entries, entry =>
            entry.EventId.Name == "AutomaticPostingDeleteIdempotent");
    }

    [Fact]
    public async Task Posting_MetricsExposeOutcomesWithLowCardinalityTags()
    {
        const string instrumentName =
            "mini_erp.accounting.automatic_posting.operations";
        var measurements = new ConcurrentQueue<MetricMeasurement>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "MiniErp.Accounting" &&
                instrument.Name == instrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((
            instrument,
            measurement,
            tags,
            _) => measurements.Enqueue(new MetricMeasurement(
                instrument.Name,
                measurement,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value))));
        listener.Start();

        await using var database = await TestDatabase.CreateAsync();
        var service = new AutomaticPostingService(
            database.Context,
            new TestCurrentCompanyContext(1),
            TimeProvider.System,
            NullLogger<AutomaticPostingService>.Instance);

        Assert.True((await service.CreateOrGetAsync(CreateRequest(100m))).IsSuccess);
        Assert.True((await service.CreateOrGetAsync(CreateRequest(100m))).IsSuccess);
        Assert.True((await service.CreateOrUpdateAsync(CreateRequest(125m))).IsSuccess);
        Assert.True((await service.DeleteAsync(
            JournalEntrySourceType.CashVoucher,
            sourceId: 42)).IsSuccess);
        Assert.True((await service.DeleteAsync(
            JournalEntrySourceType.CashVoucher,
            sourceId: 42)).IsSuccess);
        Assert.True((await service.DeleteAsync(
            JournalEntrySourceType.CashVoucher,
            sourceId: 0)).IsFailure);

        var outcomes = measurements
            .Where(metric => metric.Name == instrumentName)
            .Select(metric => metric.Tags["result"]?.ToString())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("created", outcomes);
        Assert.Contains("updated", outcomes);
        Assert.Contains("deleted", outcomes);
        Assert.Contains("idempotent", outcomes);
        Assert.Contains("failed", outcomes);
        Assert.All(measurements, metric =>
        {
            Assert.Equal(1, metric.Value);
            Assert.Equal(
                ["operation", "result", "source_type"],
                metric.Tags.Keys.Order(StringComparer.Ordinal).ToArray());
            Assert.DoesNotContain("companyId", metric.Tags.Keys);
            Assert.DoesNotContain("sourceId", metric.Tags.Keys);
        });
    }

    [Fact]
    public async Task CashVoucherPosting_UsesMappingsAndSynchronizesLifecycle()
    {
        await using var database = await TestDatabase.CreateAsync();
        var companyContext = new TestCurrentCompanyContext(1);
        var automaticPostingService = new AutomaticPostingService(
            database.Context,
            companyContext,
            TimeProvider.System,
            NullLogger<AutomaticPostingService>.Instance);
        var postingService = new CashVoucherPostingService(
            database.Context,
            companyContext,
            new AccountMappingResolver(database.Context, companyContext),
            automaticPostingService);
        var voucher = new CashVoucher
        {
            Id = 42,
            CompanyId = 1,
            VoucherNumber = "RCV-0042",
            VoucherDate = new DateOnly(2026, 8, 31),
            Direction = CashDirection.Receipt,
            CashboxId = 7,
            PartyType = CashPartyType.Partner,
            BusinessPartnerId = 11,
            Amount = 100m,
            IsPosted = true,
            Description = "تحصيل من عميل"
        };
        voucher.ApplyExchangeRate(exchangeRateId: null, exchangeRate: 1m);

        var created = await postingService.SynchronizeAsync(voucher);
        voucher.Amount = 150m;
        voucher.ApplyExchangeRate(exchangeRateId: null, exchangeRate: 1m);
        var updated = await postingService.SynchronizeAsync(voucher);

        Assert.True(created.IsSuccess);
        Assert.True(updated.IsSuccess);
        Assert.Equal(created.Value.JournalEntryId, updated.Value.JournalEntryId);
        var entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(150m, entry.Lines.Sum(line => line.Debit));
        Assert.Equal(150m, entry.Lines.Sum(line => line.Credit));
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 2 && line.Debit == 150m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 3 && line.Credit == 150m);

        var deleted = await postingService.DeleteAsync(voucher.Id);
        Assert.True(deleted.IsSuccess);
        Assert.Empty(await database.Context.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task CashboxTransferPosting_UsesOneEntryAndBooksExchangeDifference()
    {
        await using var database = await TestDatabase.CreateAsync();
        var companyContext = new TestCurrentCompanyContext(1);
        var automaticPostingService = new AutomaticPostingService(
            database.Context,
            companyContext,
            TimeProvider.System,
            NullLogger<AutomaticPostingService>.Instance);
        var postingService = new CashboxTransferPostingService(
            database.Context,
            companyContext,
            new AccountMappingResolver(database.Context, companyContext),
            automaticPostingService);

        var created = await postingService.SynchronizeAsync(50);

        Assert.True(created.IsSuccess);
        var entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(105m, entry.Lines.Sum(line => line.Debit));
        Assert.Equal(105m, entry.Lines.Sum(line => line.Credit));
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 5 && line.Credit == 5m);

        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE CashVouchers SET BaseAmount = 95 WHERE Id = 502");
        var updated = await postingService.SynchronizeAsync(50);

        Assert.True(updated.IsSuccess);
        Assert.Equal(created.Value.JournalEntryId, updated.Value.JournalEntryId);
        database.Context.ChangeTracker.Clear();
        entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(100m, entry.Lines.Sum(line => line.Debit));
        Assert.Equal(100m, entry.Lines.Sum(line => line.Credit));
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 6 && line.Debit == 5m);

        var deleted = await postingService.DeleteAsync(50);
        Assert.True(deleted.IsSuccess);
        Assert.Empty(await database.Context.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task InvoicePosting_UpdatesSameEntryAndDeletesItWithSource()
    {
        await using var database = await TestDatabase.CreateAsync();
        var companyContext = new TestCurrentCompanyContext(1);
        var automaticPostingService = new AutomaticPostingService(
            database.Context,
            companyContext,
            TimeProvider.System,
            NullLogger<AutomaticPostingService>.Instance);
        var postingService = new InvoicePostingService(
            database.Context,
            companyContext,
            new AccountMappingResolver(database.Context, companyContext),
            automaticPostingService);

        var created = await postingService.SynchronizeAsync(60);

        Assert.True(created.IsSuccess);
        var entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(JournalEntrySourceType.Invoice, entry.SourceType);
        Assert.Equal(60, entry.SourceId);
        Assert.Equal(265m, entry.Lines.Sum(line => line.Debit));
        Assert.Equal(265m, entry.Lines.Sum(line => line.Credit));
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 10 && line.Debit == 105m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 5 && line.Credit == 5m);

        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE Invoices SET Total = 120, BaseTotal = 120 WHERE Id = 60");
        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE ItemMovements SET TotalCost = 70 WHERE Id = 6001");
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE InvoicePayments
            SET AppliedBaseAmount = 120, CashboxBaseAmount = 115
            WHERE Id = 6002
            """);
        var updated = await postingService.SynchronizeAsync(60);

        Assert.True(updated.IsSuccess);
        Assert.Equal(created.Value.JournalEntryId, updated.Value.JournalEntryId);
        database.Context.ChangeTracker.Clear();
        entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(310m, entry.Lines.Sum(line => line.Debit));
        Assert.Equal(310m, entry.Lines.Sum(line => line.Credit));
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 6 && line.Debit == 5m);

        var deleted = await postingService.DeleteAsync(60);
        Assert.True(deleted.IsSuccess);
        Assert.Empty(await database.Context.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task StockAdjustmentPosting_UpdatesSameEntryAndDeletesIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var companyContext = new TestCurrentCompanyContext(1);
        var postingService = new InventoryPostingService(
            database.Context,
            companyContext,
            new AccountMappingResolver(database.Context, companyContext),
            new AutomaticPostingService(
                database.Context,
                companyContext,
                TimeProvider.System,
                NullLogger<AutomaticPostingService>.Instance));

        var created = await postingService.SynchronizeStockAdjustmentAsync(70);

        Assert.True(created.IsSuccess);
        var entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        var entryId = entry.Id;
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 8 && line.Debit == 30m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 5 && line.Credit == 30m);

        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE StockAdjustments SET Direction = 2 WHERE Id = 70");
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE ItemMovements
            SET MovementType = 7, TotalCost = 40
            WHERE Id = 7001
            """);
        var updated = await postingService.SynchronizeStockAdjustmentAsync(70);

        Assert.True(updated.IsSuccess);
        database.Context.ChangeTracker.Clear();
        entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(entryId, entry.Id);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 6 && line.Debit == 40m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 8 && line.Credit == 40m);

        var deleted = await postingService.DeleteAsync(
            JournalEntrySourceType.StockAdjustment,
            70);
        Assert.True(deleted.IsSuccess);
        Assert.Empty(await database.Context.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task StockOpeningPosting_UsesEquityAndSynchronizesLifecycle()
    {
        await using var database = await TestDatabase.CreateAsync();
        var companyContext = new TestCurrentCompanyContext(1);
        var postingService = new InventoryPostingService(
            database.Context,
            companyContext,
            new AccountMappingResolver(database.Context, companyContext),
            new AutomaticPostingService(
                database.Context,
                companyContext,
                TimeProvider.System,
                NullLogger<AutomaticPostingService>.Instance));

        var created = await postingService
            .SynchronizeStockOpeningBalanceAsync(80);

        Assert.True(created.IsSuccess);
        var entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        var entryId = entry.Id;
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 8 && line.Debit == 200m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 11 && line.Credit == 200m);

        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE ItemMovements SET TotalCost = 220 WHERE Id = 8001");
        var updated = await postingService
            .SynchronizeStockOpeningBalanceAsync(80);

        Assert.True(updated.IsSuccess);
        database.Context.ChangeTracker.Clear();
        entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(entryId, entry.Id);
        Assert.Equal(220m, entry.Lines.Sum(line => line.Debit));

        var deleted = await postingService.DeleteAsync(
            JournalEntrySourceType.StockOpeningBalance,
            80);
        Assert.True(deleted.IsSuccess);
        Assert.Empty(await database.Context.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task PartnerOpeningPosting_ChangesControlSideOnUpdate()
    {
        await using var database = await TestDatabase.CreateAsync();
        var companyContext = new TestCurrentCompanyContext(1);
        var postingService = CreateOpeningBalancePostingService(
            database,
            companyContext);

        var created = await postingService.SynchronizePartnerAsync(90);

        Assert.True(created.IsSuccess);
        var entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        var entryId = entry.Id;
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 3 && line.Debit == 300m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 11 && line.Credit == 300m);

        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE PartnerOpeningBalances
            SET BalanceType = 2, BaseAmount = 350
            WHERE Id = 90
            """);
        var updated = await postingService.SynchronizePartnerAsync(90);

        Assert.True(updated.IsSuccess);
        database.Context.ChangeTracker.Clear();
        entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(entryId, entry.Id);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 11 && line.Debit == 350m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 12 && line.Credit == 350m);

        var deleted = await postingService.DeleteAsync(
            JournalEntrySourceType.PartnerOpeningBalance,
            90);
        Assert.True(deleted.IsSuccess);
        Assert.Empty(await database.Context.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task EmployeeOpeningPosting_SkipsPayrollGeneratedBalance()
    {
        await using var database = await TestDatabase.CreateAsync();
        var companyContext = new TestCurrentCompanyContext(1);
        var postingService = CreateOpeningBalancePostingService(
            database,
            companyContext);

        var created = await postingService.SynchronizeEmployeeAsync(91);

        Assert.True(created.IsSuccess);
        var entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        var entryId = entry.Id;
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 14 && line.Debit == 80m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 11 && line.Credit == 80m);

        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE EmployeeOpeningBalances
            SET BalanceType = 2, BaseAmount = 90
            WHERE Id = 91
            """);
        var updated = await postingService.SynchronizeEmployeeAsync(91);
        var payrollGenerated = await postingService
            .SynchronizeEmployeeAsync(92);

        Assert.True(updated.IsSuccess);
        Assert.True(payrollGenerated.IsSuccess);
        database.Context.ChangeTracker.Clear();
        entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(entryId, entry.Id);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 11 && line.Debit == 90m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 13 && line.Credit == 90m);

        var deleted = await postingService.DeleteAsync(
            JournalEntrySourceType.EmployeeOpeningBalance,
            91);
        Assert.True(deleted.IsSuccess);
        Assert.Empty(await database.Context.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task CashboxOpeningPosting_UpdatesSameEntryAndDeletesIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var companyContext = new TestCurrentCompanyContext(1);
        var postingService = CreateOpeningBalancePostingService(
            database,
            companyContext);

        var created = await postingService.SynchronizeCashboxAsync(7);

        Assert.True(created.IsSuccess);
        var entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        var entryId = entry.Id;
        Assert.Equal(JournalEntrySourceType.CashboxOpeningBalance,
            entry.SourceType);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 2 && line.Debit == 500m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 11 && line.Credit == 500m);

        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE Cashboxes SET BaseOpeningBalance = -100 WHERE Id = 7");
        var updated = await postingService.SynchronizeCashboxAsync(7);

        Assert.True(updated.IsSuccess);
        database.Context.ChangeTracker.Clear();
        entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(entryId, entry.Id);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 11 && line.Debit == 100m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 2 && line.Credit == 100m);

        var deleted = await postingService.DeleteAsync(
            JournalEntrySourceType.CashboxOpeningBalance,
            7);
        Assert.True(deleted.IsSuccess);
        Assert.Empty(await database.Context.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task DriverTripPosting_UpdatesSameEntryAndZeroCostDeletesIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var companyContext = new TestCurrentCompanyContext(1);
        var postingService = new DriverTripPostingService(
            database.Context,
            companyContext,
            new AccountMappingResolver(database.Context, companyContext),
            new AutomaticPostingService(
                database.Context,
                companyContext,
                TimeProvider.System,
                NullLogger<AutomaticPostingService>.Instance));

        var created = await postingService.SynchronizeAsync(95);

        Assert.True(created.IsSuccess);
        var entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        var entryId = entry.Id;
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 9 && line.Debit == 70m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == 13 && line.Credit == 70m);

        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE DriverTrips SET Cost = 85 WHERE Id = 95");
        var updated = await postingService.SynchronizeAsync(95);

        Assert.True(updated.IsSuccess);
        database.Context.ChangeTracker.Clear();
        entry = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(journalEntry => journalEntry.Lines)
            .SingleAsync();
        Assert.Equal(entryId, entry.Id);
        Assert.Equal(85m, entry.Lines.Sum(line => line.Debit));

        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE DriverTrips SET Cost = 0 WHERE Id = 95");
        var removed = await postingService.SynchronizeAsync(95);

        Assert.True(removed.IsSuccess);
        Assert.Empty(await database.Context.JournalEntries.ToListAsync());
    }

    [Fact]
    public async Task InventoryCostPostingSynchronizer_RefreshesAllAffectedSources()
    {
        await using var database = await TestDatabase.CreateAsync();
        var companyContext = new TestCurrentCompanyContext(1);
        var mappingResolver = new AccountMappingResolver(
            database.Context,
            companyContext);
        var automaticPostingService = new AutomaticPostingService(
            database.Context,
            companyContext,
            TimeProvider.System,
            NullLogger<AutomaticPostingService>.Instance);
        var invoicePostingService = new InvoicePostingService(
            database.Context,
            companyContext,
            mappingResolver,
            automaticPostingService);
        var inventoryPostingService = new InventoryPostingService(
            database.Context,
            companyContext,
            mappingResolver,
            automaticPostingService);
        var synchronizer = new InventoryCostPostingSynchronizer(
            database.Context,
            companyContext,
            invoicePostingService,
            inventoryPostingService);

        Assert.True((await invoicePostingService.SynchronizeAsync(60)).IsSuccess);
        Assert.True((await inventoryPostingService
            .SynchronizeStockAdjustmentAsync(70)).IsSuccess);
        Assert.True((await inventoryPostingService
            .SynchronizeStockOpeningBalanceAsync(80)).IsSuccess);

        await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE ItemMovements SET TotalCost = TotalCost + 10");
        var error = await synchronizer.SynchronizeAsync(
            [new InventoryCostingKey(StoreId: 1, ItemId: 1)]);

        Assert.Null(error);
        database.Context.ChangeTracker.Clear();
        var entries = await database.Context.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .ToDictionaryAsync(entry => entry.SourceType!.Value);
        Assert.Equal(70m, entries[JournalEntrySourceType.Invoice]
            .Lines.Where(line => line.AccountId == 9)
            .Sum(line => line.Debit));
        Assert.Equal(40m, entries[JournalEntrySourceType.StockAdjustment]
            .Lines.Sum(line => line.Debit));
        Assert.Equal(210m, entries[JournalEntrySourceType.StockOpeningBalance]
            .Lines.Sum(line => line.Debit));
    }

    private static OpeningBalancePostingService
        CreateOpeningBalancePostingService(
            TestDatabase database,
            TestCurrentCompanyContext companyContext) =>
        new(
            database.Context,
            companyContext,
            new AccountMappingResolver(database.Context, companyContext),
            new AutomaticPostingService(
                database.Context,
                companyContext,
                TimeProvider.System,
                NullLogger<AutomaticPostingService>.Instance));

    private static AutomaticJournalEntryRequest CreateRequest(decimal amount) =>
        new(
            FiscalYearId: 1,
            EntryDate: new DateOnly(2026, 8, 31),
            Description: "سند قبض RCV-0042",
            SourceType: JournalEntrySourceType.CashVoucher,
            SourceId: 42,
            SourceNumber: "RCV-0042",
            Lines:
            [
                new JournalEntryLineRequest(
                    AccountId: 2,
                    Description: "الخزينة",
                    Debit: amount,
                    Credit: 0m),
                new JournalEntryLineRequest(
                    AccountId: 3,
                    Description: "الطرف المقابل",
                    Debit: 0m,
                    Credit: amount)
            ]);

    private sealed class TestDatabase(
        SqliteConnection connection,
        ApplicationDbContext context) : IAsyncDisposable
    {
        public ApplicationDbContext Context { get; } = context;

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await CreateSchemaAsync(context);
            return new TestDatabase(connection, context);
        }

        private static async Task CreateSchemaAsync(
            ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE FiscalYears (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    IsCurrent INTEGER NOT NULL,
                    ClosedOn TEXT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL DEFAULT '',
                    CreatedOn TEXT NOT NULL DEFAULT '0001-01-01',
                    CreatedByPc TEXT NOT NULL DEFAULT '',
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

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
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL DEFAULT '',
                    CreatedOn TEXT NOT NULL DEFAULT '0001-01-01',
                    CreatedByPc TEXT NOT NULL DEFAULT '',
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
                    CreatedById TEXT NOT NULL DEFAULT '',
                    CreatedOn TEXT NOT NULL DEFAULT '0001-01-01',
                    CreatedByPc TEXT NOT NULL DEFAULT '',
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE CashboxTransfers (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    TransferNumber TEXT NOT NULL,
                    TransferDate TEXT NOT NULL,
                    SourceCashboxId INTEGER NOT NULL,
                    DestinationCashboxId INTEGER NOT NULL,
                    Description TEXT NULL,
                    Notes TEXT NULL,
                    LastModifiedAt TEXT NOT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL DEFAULT '',
                    CreatedOn TEXT NOT NULL DEFAULT '0001-01-01',
                    CreatedByPc TEXT NOT NULL DEFAULT '',
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE Cashboxes (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    OpeningBalanceDate TEXT NOT NULL,
                    BaseOpeningBalance TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE CashVouchers (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    CashboxTransferId INTEGER NULL,
                    Direction INTEGER NOT NULL,
                    CashboxId INTEGER NULL,
                    Amount TEXT NOT NULL,
                    ExchangeRate TEXT NOT NULL,
                    BaseAmount TEXT NOT NULL,
                    IsPosted INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE Invoices (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    InvoiceNumber TEXT NOT NULL,
                    InvoiceDate TEXT NOT NULL,
                    InvoiceType INTEGER NOT NULL,
                    Total TEXT NOT NULL,
                    ExchangeRate TEXT NOT NULL,
                    BaseTotal TEXT NOT NULL,
                    Notes TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE ItemMovements (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    MovementType INTEGER NOT NULL,
                    ReferenceId INTEGER NOT NULL,
                    TotalCost TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE StockAdjustments (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    DocumentNumber TEXT NOT NULL,
                    DocumentDate TEXT NOT NULL,
                    Direction INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE StockOpeningBalances (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    DocumentNumber TEXT NOT NULL,
                    DocumentDate TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE PartnerOpeningBalances (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    DocumentNumber TEXT NOT NULL,
                    DocumentDate TEXT NOT NULL,
                    BalanceType INTEGER NOT NULL,
                    BaseAmount TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE EmployeeOpeningBalances (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    PayrollEntryId INTEGER NULL,
                    DocumentNumber TEXT NOT NULL,
                    DocumentDate TEXT NOT NULL,
                    BalanceType INTEGER NOT NULL,
                    BaseAmount TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE DriverTrips (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    InvoiceNumber TEXT NOT NULL,
                    TripDate TEXT NOT NULL,
                    Cost TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE InvoicePayments (
                    Id INTEGER PRIMARY KEY,
                    CompanyId INTEGER NOT NULL,
                    InvoiceId INTEGER NOT NULL,
                    CashVoucherId INTEGER NOT NULL,
                    AppliedBaseAmount TEXT NOT NULL,
                    CashboxBaseAmount TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE AccountMappings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    FiscalYearId INTEGER NOT NULL,
                    MappingType INTEGER NOT NULL,
                    SourceId INTEGER NULL,
                    AccountId INTEGER NOT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL DEFAULT '',
                    CreatedOn TEXT NOT NULL DEFAULT '0001-01-01',
                    CreatedByPc TEXT NOT NULL DEFAULT '',
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
                    Description TEXT NULL,
                    Debit TEXT NOT NULL,
                    Credit TEXT NOT NULL,
                    CreatedById TEXT NOT NULL DEFAULT '',
                    CreatedOn TEXT NOT NULL DEFAULT '0001-01-01',
                    CreatedByPc TEXT NOT NULL DEFAULT '',
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE UNIQUE INDEX UX_JournalEntries_AutomaticSource
                    ON JournalEntries (CompanyId, SourceType, SourceId)
                    WHERE EntryType = 4
                      AND ReversalOfEntryId IS NULL
                      AND SourceType IS NOT NULL
                      AND SourceId IS NOT NULL
                      AND Status = 1
                      AND IsDeleted = 0;

                INSERT INTO FiscalYears
                    (Id, CompanyId, Name, StartDate, EndDate, Status, IsCurrent)
                VALUES
                    (1, 1, '2026', '2026-01-01', '2026-12-31', 1, 1);

                INSERT INTO Accounts
                    (Id, CompanyId, Code, Name, ParentAccountId,
                     AccountType, NormalBalance, IsPosting, IsActive)
                VALUES
                    (1, 1, '1000', 'Assets', NULL, 1, 1, 0, 1),
                    (2, 1, '1110', 'Cashbox', 1, 1, 1, 1, 1),
                    (3, 1, '1200', 'Customers', 1, 1, 1, 1, 1),
                    (4, 1, '1120', 'Destination cashbox', 1, 1, 1, 1, 1),
                    (5, 1, '4200', 'Exchange gain', 1, 4, 2, 1, 1),
                    (6, 1, '5200', 'Exchange loss', 1, 5, 1, 1, 1),
                    (7, 1, '4100', 'Sales', 1, 4, 2, 1, 1),
                    (8, 1, '1300', 'Inventory', 1, 1, 1, 1, 1),
                    (9, 1, '5100', 'Cost of goods sold', 1, 5, 1, 1, 1),
                    (10, 1, '1130', 'Invoice cashbox', 1, 1, 1, 1, 1),
                    (11, 1, '3100', 'Opening equity', 1, 3, 2, 1, 1),
                    (12, 1, '2100', 'Suppliers', 1, 2, 2, 1, 1),
                    (13, 1, '2200', 'Employee payable', 1, 2, 2, 1, 1),
                    (14, 1, '1210', 'Employee receivable', 1, 1, 1, 1, 1);

                INSERT INTO AccountMappings
                    (CompanyId, FiscalYearId, MappingType, SourceId, AccountId)
                VALUES
                    (1, 1, 1, 7, 2),
                    (1, 1, 1, 8, 4),
                    (1, 1, 1, 9, 10),
                    (1, 1, 3, NULL, 7),
                    (1, 1, 7, NULL, 8),
                    (1, 1, 8, NULL, 9),
                    (1, 1, 9, NULL, 3),
                    (1, 1, 10, NULL, 12),
                    (1, 1, 11, NULL, 13),
                    (1, 1, 12, NULL, 13),
                    (1, 1, 13, NULL, 5),
                    (1, 1, 14, NULL, 6),
                    (1, 1, 15, NULL, 5),
                    (1, 1, 16, NULL, 6),
                    (1, 1, 17, NULL, 11),
                    (1, 1, 18, NULL, 14),
                    (1, 1, 19, NULL, 9);

                INSERT INTO CashboxTransfers
                    (Id, CompanyId, TransferNumber, TransferDate,
                     SourceCashboxId, DestinationCashboxId, Description,
                     LastModifiedAt)
                VALUES
                    (50, 1, 'TRF-0050', '2026-08-31', 7, 8,
                     'Currency transfer', '2026-08-31');

                INSERT INTO Cashboxes
                    (Id, CompanyId, Code, Name, OpeningBalanceDate,
                     BaseOpeningBalance)
                VALUES
                    (7, 1, 'CBX-0007', 'Main cashbox', '2026-01-01', 500);

                INSERT INTO CashVouchers
                    (Id, CompanyId, CashboxTransferId, Direction, CashboxId,
                     Amount, ExchangeRate, BaseAmount, IsPosted)
                VALUES
                    (501, 1, 50, 2, 7, 100, 1, 100, 1),
                    (502, 1, 50, 1, 8, 105, 1, 105, 1),
                    (601, 1, NULL, 1, 9, 105, 1, 105, 1);

                INSERT INTO Invoices
                    (Id, CompanyId, InvoiceNumber, InvoiceDate, InvoiceType,
                     Total, ExchangeRate, BaseTotal, Notes)
                VALUES
                    (60, 1, 'INV-0060', '2026-08-31', 1,
                     100, 1, 100, 'Sales invoice');

                INSERT INTO ItemMovements
                    (Id, CompanyId, StoreId, ItemId, MovementType,
                     ReferenceId, TotalCost)
                VALUES
                    (6001, 1, 1, 1, 1, 60, 60),
                    (7001, 1, 1, 1, 6, 70, 30),
                    (8001, 1, 1, 1, 5, 80, 200);

                INSERT INTO StockAdjustments
                    (Id, CompanyId, DocumentNumber, DocumentDate, Direction)
                VALUES
                    (70, 1, 'ADJ-0070', '2026-08-31', 1);

                INSERT INTO StockOpeningBalances
                    (Id, CompanyId, DocumentNumber, DocumentDate)
                VALUES
                    (80, 1, 'OPEN-0080', '2026-01-01');

                INSERT INTO PartnerOpeningBalances
                    (Id, CompanyId, DocumentNumber, DocumentDate,
                     BalanceType, BaseAmount)
                VALUES
                    (90, 1, 'POB-0090', '2026-01-01', 1, 300);

                INSERT INTO EmployeeOpeningBalances
                    (Id, CompanyId, PayrollEntryId, DocumentNumber,
                     DocumentDate, BalanceType, BaseAmount)
                VALUES
                    (91, 1, NULL, 'EOB-0091', '2026-01-01', 1, 80),
                    (92, 1, 500, 'EOB-0092', '2026-08-31', 2, 500);

                INSERT INTO DriverTrips
                    (Id, CompanyId, InvoiceNumber, TripDate, Cost)
                VALUES
                    (95, 1, 'INV-0095', '2026-08-31', 70);

                INSERT INTO InvoicePayments
                    (Id, CompanyId, InvoiceId, CashVoucherId,
                     AppliedBaseAmount, CashboxBaseAmount)
                VALUES
                    (6002, 1, 60, 601, 100, 105);
                """);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record MetricMeasurement(
        string Name,
        long Value,
        IReadOnlyDictionary<string, object?> Tags);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<LogEntry> entries = new();

        public IReadOnlyCollection<LogEntry> Entries => entries.ToArray();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(
                logLevel,
                eventId,
                formatter(state, exception)));
    }

    private sealed record LogEntry(
        LogLevel Level,
        EventId EventId,
        string Message);

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
