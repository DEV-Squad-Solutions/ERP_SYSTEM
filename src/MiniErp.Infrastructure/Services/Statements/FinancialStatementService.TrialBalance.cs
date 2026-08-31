using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using static MiniErp.Application.Features.Statements.StatementErrors;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService
{
    public async Task<Result<TrialBalanceResponse>> GetTrialBalanceAsync(
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                (!filters.FiscalYearId.HasValue ||
                 year.Id == filters.FiscalYearId.Value) &&
                year.StartDate <= filters.FromDate &&
                year.EndDate >= filters.ToDate)
            .OrderByDescending(year => year.IsCurrent)
            .ThenBy(year => year.StartDate)
            .Select(year => new
            {
                year.Id,
                year.Name,
                year.StartDate,
                year.EndDate
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (fiscalYear is null)
        {
            return Result<TrialBalanceResponse>.Failure(
                FiscalYearNotFound(filters.FiscalYearId));
        }

        var accountRows = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                account.IsActive &&
                account.IsPosting)
            .Select(account => new TrialBalanceAccountRow
            {
                Id = account.Id,
                Code = account.Code,
                Name = account.Name,
                AccountType = account.AccountType
            })
            .ToListAsync(cancellationToken);

        var mappings = await dbContext.AccountMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYear.Id)
            .Select(mapping => new TrialBalanceMappingRow
            {
                MappingType = mapping.MappingType,
                SourceId = mapping.SourceId,
                AccountId = mapping.AccountId
            })
            .ToListAsync(cancellationToken);

        var ledger = new TrialBalanceLedger(
            accountRows,
            mappings,
            filters.FromDate);

        await LoadCashboxesAsync(
            ledger,
            filters,
            cancellationToken);
        await LoadCashVouchersAsync(
            ledger,
            filters,
            cancellationToken);
        await LoadPartnerOpeningBalancesAsync(
            ledger,
            filters,
            cancellationToken);
        await LoadLegacyPartnerMovementsAsync(
            ledger,
            filters,
            cancellationToken);
        await LoadInvoicesAsync(
            ledger,
            filters,
            cancellationToken);
        await LoadInventoryCostMovementsAsync(
            ledger,
            filters,
            cancellationToken);
        await LoadDriverTripsAsync(
            ledger,
            filters,
            cancellationToken);
        await LoadEmployeeBalancesAsync(
            ledger,
            filters,
            cancellationToken);
        await LoadJournalEntriesAsync(
            ledger,
            fiscalYear.Id,
            filters,
            cancellationToken);

        var items = ledger.ToItems(
            filters.IncludeZeroBalances,
            filters.IncludeUnclassified,
            filters.ViewMode);
        var totals = new TrialBalanceTotalsResponse(
            OpeningDebit: items.Sum(item => item.OpeningDebit),
            OpeningCredit: items.Sum(item => item.OpeningCredit),
            PeriodDebit: items.Sum(item => item.PeriodDebit),
            PeriodCredit: items.Sum(item => item.PeriodCredit),
            ClosingDebit: items.Sum(item => item.ClosingDebit),
            ClosingCredit: items.Sum(item => item.ClosingCredit));

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

        return Result<TrialBalanceResponse>.Success(
            new TrialBalanceResponse(
                FiscalYearId: fiscalYear.Id,
                FiscalYearName: fiscalYear.Name,
                FromDate: filters.FromDate,
                ToDate: filters.ToDate,
                BaseCurrency: baseCurrency,
                ViewMode: filters.ViewMode,
                AdjustmentView: filters.AdjustmentView,
                IsOperationalOnly: false,
                Items: items,
                Totals: totals));
    }

    private async Task LoadJournalEntriesAsync(
        TrialBalanceLedger ledger,
        int fiscalYearId,
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var includeAdjustments = filters.AdjustmentView ==
            TrialBalanceAdjustmentView.AfterAdjustments;
        var lines = await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.JournalEntry.FiscalYearId == fiscalYearId &&
                line.JournalEntry.EntryDate <= filters.ToDate &&
                (includeAdjustments ||
                 line.JournalEntry.EntryType != JournalEntryType.Adjustment))
            .Select(line => new
            {
                line.JournalEntry.EntryDate,
                line.AccountId,
                line.Debit,
                line.Credit
            })
            .ToListAsync(cancellationToken);

        foreach (var line in lines)
        {
            ledger.Add(
                line.AccountId,
                line.EntryDate,
                line.Debit,
                line.Credit);
        }
    }

    private async Task LoadCashboxesAsync(
        TrialBalanceLedger ledger,
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.OpeningBalanceDate <= filters.ToDate)
            .Select(cashbox => new
            {
                cashbox.Id,
                cashbox.OpeningBalanceDate,
                cashbox.BaseOpeningBalance
            })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            ledger.AddSigned(
                ledger.Resolve(AccountingMappingType.Cashbox, row.Id),
                row.OpeningBalanceDate,
                row.BaseOpeningBalance);
        }
    }

    private async Task LoadCashVouchersAsync(
        TrialBalanceLedger ledger,
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var vouchers = await dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.IsPosted &&
                voucher.VoucherDate <= filters.ToDate)
            .Select(voucher => new TrialBalanceVoucherRow
            {
                Id = voucher.Id,
                VoucherDate = voucher.VoucherDate,
                Direction = voucher.Direction,
                CashboxId = voucher.CashboxId,
                CashboxTransferId = voucher.CashboxTransferId,
                CashMovementTypeId = voucher.CashMovementTypeId,
                MovementClassification = voucher.CashMovementType == null
                    ? null
                    : voucher.CashMovementType.Classification,
                PartnerEffect = voucher.CashMovementType == null
                    ? PartnerAccountEffect.None
                    : voucher.CashMovementType.PartnerEffect,
                AccountId = voucher.AccountId,
                PartyType = voucher.PartyType,
                EmployeeId = voucher.EmployeeId,
                BusinessPartnerId = voucher.BusinessPartnerId,
                DriverId = voucher.DriverId,
                BaseAmount = voucher.BaseAmount,
                Amount = voucher.Amount,
                ExchangeRate = voucher.ExchangeRate
            })
            .ToListAsync(cancellationToken);

        foreach (var voucher in vouchers)
        {
            var amount = GetBaseAmount(
                voucher.BaseAmount,
                voucher.Amount,
                voucher.ExchangeRate);
            var isReceipt = voucher.Direction == CashDirection.Receipt;

            ledger.Add(
                ledger.Resolve(AccountingMappingType.Cashbox, voucher.CashboxId),
                voucher.VoucherDate,
                debit: isReceipt ? amount : 0m,
                credit: isReceipt ? 0m : amount);

            // The two sides of a cashbox transfer are already represented by
            // their two cashboxes; there is no external counterpart to add.
            if (voucher.CashboxTransferId.HasValue)
            {
                continue;
            }

            int? counterpartAccountId = voucher.AccountId;
            if (!counterpartAccountId.HasValue)
            {
                counterpartAccountId = ResolveVoucherCounterpart(
                    ledger,
                    voucher);
            }

            ledger.Add(
                counterpartAccountId,
                voucher.VoucherDate,
                debit: isReceipt ? 0m : amount,
                credit: isReceipt ? amount : 0m);
        }
    }

    private static int? ResolveVoucherCounterpart(
        TrialBalanceLedger ledger,
        TrialBalanceVoucherRow voucher)
    {
        if (voucher.PartyType == CashPartyType.Partner &&
            voucher.CashMovementTypeId.HasValue &&
            voucher.MovementClassification ==
            CashMovementClassification.PartnerSettlement)
        {
            var movementAccount = ledger.Resolve(
                AccountingMappingType.CashMovementType,
                voucher.CashMovementTypeId);
            if (movementAccount.HasValue)
            {
                return movementAccount;
            }
        }

        return voucher.PartyType switch
        {
            CashPartyType.Partner => voucher.PartnerEffect switch
            {
                PartnerAccountEffect.Credit => ledger.Resolve(
                    AccountingMappingType.CustomerControl,
                    sourceId: null),
                PartnerAccountEffect.Debit => ledger.Resolve(
                    AccountingMappingType.SupplierControl,
                    sourceId: null),
                _ => voucher.Direction == CashDirection.Receipt
                    ? ledger.Resolve(
                        AccountingMappingType.CustomerControl,
                        sourceId: null)
                    : ledger.Resolve(
                        AccountingMappingType.SupplierControl,
                        sourceId: null)
            },
            CashPartyType.Driver => ledger.Resolve(
                AccountingMappingType.DriverControl,
                sourceId: null),
            CashPartyType.Employee => ledger.Resolve(
                AccountingMappingType.EmployeeControl,
                sourceId: null),
            CashPartyType.None when voucher.CashMovementTypeId.HasValue =>
                ledger.Resolve(
                    AccountingMappingType.CashMovementType,
                    voucher.CashMovementTypeId),
            _ => null
        };
    }

    private async Task LoadPartnerOpeningBalancesAsync(
        TrialBalanceLedger ledger,
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var balances = await dbContext.PartnerOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.DocumentDate <= filters.ToDate)
            .Select(balance => new
            {
                balance.DocumentDate,
                balance.BalanceType,
                balance.BaseAmount,
                balance.Amount,
                balance.ExchangeRate
            })
            .ToListAsync(cancellationToken);

        foreach (var balance in balances)
        {
            var accountType = balance.BalanceType == PartnerBalanceType.Receivable
                ? AccountingMappingType.CustomerControl
                : AccountingMappingType.SupplierControl;
            var amount = GetBaseAmount(
                balance.BaseAmount,
                balance.Amount,
                balance.ExchangeRate);
            ledger.Add(
                ledger.Resolve(accountType, sourceId: null),
                balance.DocumentDate,
                debit: balance.BalanceType == PartnerBalanceType.Receivable
                    ? amount
                    : 0m,
                credit: balance.BalanceType == PartnerBalanceType.Payable
                    ? amount
                    : 0m);
        }
    }

    private async Task LoadLegacyPartnerMovementsAsync(
        TrialBalanceLedger ledger,
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var movements = await dbContext.BusinessPartnerMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.InvoiceId == null &&
                movement.CashVoucherId == null &&
                movement.MovementDate <= filters.ToDate)
            .Select(movement => new
            {
                movement.MovementDate,
                movement.MovementType,
                movement.BaseDebit,
                movement.BaseCredit,
                movement.Debit,
                movement.Credit,
                movement.ExchangeRate
            })
            .ToListAsync(cancellationToken);

        foreach (var movement in movements)
        {
            var controlType = movement.MovementType switch
            {
                BusinessPartnerMovementType.Sales or
                BusinessPartnerMovementType.SalesReturn or
                BusinessPartnerMovementType.CashReceipt =>
                    AccountingMappingType.CustomerControl,
                _ => AccountingMappingType.SupplierControl
            };
            ledger.Add(
                ledger.Resolve(controlType, sourceId: null),
                movement.MovementDate,
                movement.BaseDebit != 0m || movement.Debit == 0m
                    ? movement.BaseDebit
                    : GetBaseAmount(
                        movement.BaseDebit,
                        movement.Debit,
                        movement.ExchangeRate),
                movement.BaseCredit != 0m || movement.Credit == 0m
                    ? movement.BaseCredit
                    : GetBaseAmount(
                        movement.BaseCredit,
                        movement.Credit,
                        movement.ExchangeRate));
        }
    }

    private async Task LoadInvoicesAsync(
        TrialBalanceLedger ledger,
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                invoice.InvoiceDate <= filters.ToDate)
            .Select(invoice => new
            {
                invoice.Id,
                invoice.InvoiceDate,
                invoice.InvoiceType,
                invoice.Total,
                invoice.BaseTotal,
                invoice.ExchangeRate
            })
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            var amount = GetBaseAmount(
                invoice.BaseTotal,
                invoice.Total,
                invoice.ExchangeRate);
            if (amount <= 0m)
            {
                continue;
            }

            var (invoiceMapping, controlMapping, debitInvoice, creditInvoice) =
                invoice.InvoiceType switch
                {
                    InvoiceType.Sales => (
                        AccountingMappingType.Sales,
                        AccountingMappingType.CustomerControl,
                        0m,
                        amount),
                    InvoiceType.SalesReturn => (
                        AccountingMappingType.SalesReturn,
                        AccountingMappingType.CustomerControl,
                        amount,
                        0m),
                    InvoiceType.Purchase => (
                        AccountingMappingType.Purchase,
                        AccountingMappingType.SupplierControl,
                        amount,
                        0m),
                    InvoiceType.PurchaseReturn => (
                        AccountingMappingType.PurchaseReturn,
                        AccountingMappingType.SupplierControl,
                        0m,
                        amount),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(invoice.InvoiceType),
                        invoice.InvoiceType,
                        null)
                };

            ledger.Add(
                ledger.Resolve(invoiceMapping, sourceId: null),
                invoice.InvoiceDate,
                debitInvoice,
                creditInvoice);

            ledger.Add(
                ledger.Resolve(controlMapping, sourceId: null),
                invoice.InvoiceDate,
                debit: creditInvoice,
                credit: debitInvoice);
        }
    }

    private async Task LoadInventoryCostMovementsAsync(
        TrialBalanceLedger ledger,
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var movements = await dbContext.ItemMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.MovementDate <= filters.ToDate &&
                movement.TotalCost != 0m)
            .Select(movement => new
            {
                movement.MovementDate,
                movement.MovementType,
                movement.TotalCost
            })
            .ToListAsync(cancellationToken);

        foreach (var movement in movements)
        {
            switch (movement.MovementType)
            {
                case ItemMovementType.Sales:
                    AddInventoryCostPair(
                        ledger,
                        movement.MovementDate,
                        movement.TotalCost,
                        debitCost: true);
                    break;
                case ItemMovementType.SalesReturn:
                    AddInventoryCostPair(
                        ledger,
                        movement.MovementDate,
                        movement.TotalCost,
                        debitCost: false);
                    break;
                case ItemMovementType.OpeningBalance:
                    ledger.Add(
                        ledger.Resolve(
                            AccountingMappingType.Inventory,
                            sourceId: null),
                        movement.MovementDate,
                        debit: movement.TotalCost,
                        credit: 0m);
                    break;
                case ItemMovementType.AdjustmentIncrease:
                    ledger.Add(
                        ledger.Resolve(
                            AccountingMappingType.Inventory,
                            sourceId: null),
                        movement.MovementDate,
                        debit: movement.TotalCost,
                        credit: 0m);
                    break;
                case ItemMovementType.AdjustmentDecrease:
                    ledger.Add(
                        ledger.Resolve(
                            AccountingMappingType.Inventory,
                            sourceId: null),
                        movement.MovementDate,
                        debit: 0m,
                        credit: movement.TotalCost);
                    break;
            }
        }
    }

    private static void AddInventoryCostPair(
        TrialBalanceLedger ledger,
        DateOnly date,
        decimal amount,
        bool debitCost)
    {
        ledger.Add(
            ledger.Resolve(
                AccountingMappingType.CostOfGoodsSold,
                sourceId: null),
            date,
            debit: debitCost ? amount : 0m,
            credit: debitCost ? 0m : amount);
        ledger.Add(
            ledger.Resolve(
                AccountingMappingType.Inventory,
                sourceId: null),
            date,
            debit: debitCost ? 0m : amount,
            credit: debitCost ? amount : 0m);
    }

    private async Task LoadDriverTripsAsync(
        TrialBalanceLedger ledger,
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var trips = await dbContext.DriverTrips
            .AsNoTracking()
            .Where(trip =>
                trip.CompanyId == companyId &&
                trip.TripDate <= filters.ToDate &&
                trip.Cost.HasValue &&
                trip.Cost.Value != 0m)
            .Select(trip => new
            {
                trip.TripDate,
                Cost = trip.Cost!.Value
            })
            .ToListAsync(cancellationToken);

        foreach (var trip in trips)
        {
            ledger.Add(
                ledger.Resolve(
                    AccountingMappingType.CostOfGoodsSold,
                    sourceId: null),
                trip.TripDate,
                debit: trip.Cost,
                credit: 0m);
            ledger.Add(
                ledger.Resolve(
                    AccountingMappingType.DriverControl,
                    sourceId: null),
                trip.TripDate,
                debit: 0m,
                credit: trip.Cost);
        }
    }

    private async Task LoadEmployeeBalancesAsync(
        TrialBalanceLedger ledger,
        TrialBalanceFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var openingBalances = await dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.DocumentDate <= filters.ToDate)
            .Select(balance => new
            {
                balance.DocumentDate,
                balance.BalanceType,
                balance.BaseAmount,
                balance.Amount,
                balance.ExchangeRate
            })
            .ToListAsync(cancellationToken);

        foreach (var balance in openingBalances)
        {
            var amount = GetBaseAmount(
                balance.BaseAmount,
                balance.Amount,
                balance.ExchangeRate);
            ledger.Add(
                ledger.Resolve(
                    AccountingMappingType.EmployeeControl,
                    sourceId: null),
                balance.DocumentDate,
                debit: balance.BalanceType == EmployeeBalanceType.Debit
                    ? amount
                    : 0m,
                credit: balance.BalanceType == EmployeeBalanceType.Credit
                    ? amount
                    : 0m);
        }

        var movements = await dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.CashVoucherId == null &&
                movement.MovementDate <= filters.ToDate)
            .Select(movement => new
            {
                movement.MovementDate,
                movement.BaseDebit,
                movement.BaseCredit,
                movement.Debit,
                movement.Credit,
                movement.ExchangeRate
            })
            .ToListAsync(cancellationToken);

        foreach (var movement in movements)
        {
            var debit = movement.BaseDebit != 0m || movement.Debit == 0m
                ? movement.BaseDebit
                : GetBaseAmount(
                    movement.BaseDebit,
                    movement.Debit,
                    movement.ExchangeRate);
            var credit = movement.BaseCredit != 0m || movement.Credit == 0m
                ? movement.BaseCredit
                : GetBaseAmount(
                    movement.BaseCredit,
                    movement.Credit,
                    movement.ExchangeRate);
            ledger.Add(
                ledger.Resolve(
                    AccountingMappingType.EmployeeControl,
                    sourceId: null),
                movement.MovementDate,
                debit,
                credit);
        }
    }

    private static decimal GetBaseAmount(
        decimal baseAmount,
        decimal amount,
        decimal exchangeRate) =>
        baseAmount != 0m || amount == 0m
            ? baseAmount
            : decimal.Round(
                amount * exchangeRate,
                8,
                MidpointRounding.AwayFromZero);

    private sealed class TrialBalanceLedger
    {
        private readonly DateOnly fromDate;
        private readonly Dictionary<int, TrialBalanceAccountBalance> accounts;
        private readonly Dictionary<TrialBalanceMappingKey, int> mappings;
        private readonly TrialBalanceAccountBalance unclassified =
            new(
                accountId: null,
                accountCode: null,
                accountName: "غير مصنف",
                accountType: null,
                isUnclassified: true);

        public TrialBalanceLedger(
            IReadOnlyCollection<TrialBalanceAccountRow> accountRows,
            IReadOnlyCollection<TrialBalanceMappingRow> mappingRows,
            DateOnly fromDate)
        {
            this.fromDate = fromDate;
            accounts = accountRows.ToDictionary(
                row => row.Id,
                row => new TrialBalanceAccountBalance(
                    accountId: row.Id,
                    accountCode: row.Code,
                    accountName: row.Name,
                    accountType: row.AccountType,
                    isUnclassified: false));
            mappings = mappingRows.ToDictionary(
                row => new TrialBalanceMappingKey(
                    row.MappingType,
                    row.SourceId),
                row => row.AccountId);
        }

        public int? Resolve(
            AccountingMappingType mappingType,
            int? sourceId) =>
            mappings.TryGetValue(
                new TrialBalanceMappingKey(mappingType, sourceId),
                out var accountId)
                ? accountId
                : null;

        public void Add(
            int? accountId,
            DateOnly date,
            decimal debit,
            decimal credit)
        {
            var account = accountId.HasValue &&
                          accounts.TryGetValue(accountId.Value, out var value)
                ? value
                : unclassified;
            if (date < fromDate)
            {
                account.OpeningDebit += debit;
                account.OpeningCredit += credit;
            }
            else
            {
                account.PeriodDebit += debit;
                account.PeriodCredit += credit;
            }
        }

        public void AddSigned(
            int? accountId,
            DateOnly date,
            decimal signedAmount)
        {
            Add(
                accountId,
                date,
                debit: signedAmount >= 0m ? signedAmount : 0m,
                credit: signedAmount < 0m ? -signedAmount : 0m);
        }

        public IReadOnlyList<TrialBalanceItemResponse> ToItems(
            bool includeZeroBalances,
            bool includeUnclassified,
            TrialBalanceViewMode viewMode)
        {
            var rows = accounts.Values
                .Append(unclassified)
                .Where(row =>
                    includeZeroBalances ||
                    !row.IsZero)
                .Where(row => includeUnclassified || !row.IsUnclassified)
                .Select(ToItem)
                .OrderBy(row => row.IsUnclassified)
                .ThenBy(row => row.AccountCode)
                .ThenBy(row => row.AccountName)
                .ToArray();

            if (viewMode != TrialBalanceViewMode.Summary)
            {
                return rows;
            }

            return rows
                .GroupBy(row => new
                {
                    row.IsUnclassified,
                    row.AccountType
                })
                .Select(group => new TrialBalanceItemResponse(
                    AccountId: null,
                    AccountCode: null,
                    AccountName: group.Key.IsUnclassified
                        ? "غير مصنف"
                        : AccountTypeName(group.Key.AccountType!.Value),
                    AccountType: group.Key.AccountType,
                    IsUnclassified: group.Key.IsUnclassified,
                    OpeningDebit: group.Sum(row => row.OpeningDebit),
                    OpeningCredit: group.Sum(row => row.OpeningCredit),
                    PeriodDebit: group.Sum(row => row.PeriodDebit),
                    PeriodCredit: group.Sum(row => row.PeriodCredit),
                    ClosingDebit: group.Sum(row => row.ClosingDebit),
                    ClosingCredit: group.Sum(row => row.ClosingCredit)))
                .OrderBy(row => row.IsUnclassified)
                .ThenBy(row => row.AccountType)
                .ToArray();
        }

        private static TrialBalanceItemResponse ToItem(
            TrialBalanceAccountBalance row)
        {
            var openingSigned = row.OpeningDebit - row.OpeningCredit;
            var closingSigned = openingSigned +
                row.PeriodDebit -
                row.PeriodCredit;
            return new TrialBalanceItemResponse(
                AccountId: row.AccountId,
                AccountCode: row.AccountCode,
                AccountName: row.AccountName,
                AccountType: row.AccountType,
                IsUnclassified: row.IsUnclassified,
                OpeningDebit: row.OpeningDebit,
                OpeningCredit: row.OpeningCredit,
                PeriodDebit: row.PeriodDebit,
                PeriodCredit: row.PeriodCredit,
                ClosingDebit: Math.Max(closingSigned, 0m),
                ClosingCredit: Math.Max(-closingSigned, 0m));
        }

        private static string AccountTypeName(AccountType type) => type switch
        {
            AccountType.Asset => "الأصول",
            AccountType.Liability => "الالتزامات",
            AccountType.Equity => "حقوق الملكية",
            AccountType.Revenue => "الإيرادات",
            AccountType.Expense => "المصروفات",
            _ => type.ToString()
        };
    }

    private sealed class TrialBalanceAccountRow
    {
        public int Id { get; init; }

        public string Code { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public AccountType AccountType { get; init; }
    }

    private sealed class TrialBalanceMappingRow
    {
        public AccountingMappingType MappingType { get; init; }

        public int? SourceId { get; init; }

        public int AccountId { get; init; }
    }

    private sealed class TrialBalanceVoucherRow
    {
        public int Id { get; init; }

        public DateOnly VoucherDate { get; init; }

        public CashDirection Direction { get; init; }

        public int? CashboxId { get; init; }

        public int? CashboxTransferId { get; init; }

        public int? CashMovementTypeId { get; init; }

        public CashMovementClassification? MovementClassification { get; init; }

        public PartnerAccountEffect PartnerEffect { get; init; }

        public int? AccountId { get; init; }

        public CashPartyType PartyType { get; init; }

        public int? EmployeeId { get; init; }

        public int? BusinessPartnerId { get; init; }

        public int? DriverId { get; init; }

        public decimal BaseAmount { get; init; }

        public decimal Amount { get; init; }

        public decimal ExchangeRate { get; init; }
    }

    private sealed class TrialBalanceAccountBalance(
        int? accountId,
        string? accountCode,
        string accountName,
        AccountType? accountType,
        bool isUnclassified)
    {
        public int? AccountId { get; } = accountId;

        public string? AccountCode { get; } = accountCode;

        public string AccountName { get; } = accountName;

        public AccountType? AccountType { get; } = accountType;

        public bool IsUnclassified { get; } = isUnclassified;

        public decimal OpeningDebit { get; set; }

        public decimal OpeningCredit { get; set; }

        public decimal PeriodDebit { get; set; }

        public decimal PeriodCredit { get; set; }

        public bool IsZero =>
            OpeningDebit == 0m &&
            OpeningCredit == 0m &&
            PeriodDebit == 0m &&
            PeriodCredit == 0m;
    }

    private sealed record TrialBalanceMappingKey(
        AccountingMappingType MappingType,
        int? SourceId);
}
