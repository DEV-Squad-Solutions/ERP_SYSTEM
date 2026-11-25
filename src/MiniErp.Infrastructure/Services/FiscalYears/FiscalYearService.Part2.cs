using System.Data;
using System.Globalization;
using Mapster;
using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.Companies;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.DriverOpeningBalances;
using MiniErp.Infrastructure.Services.ExchangeRates;

namespace MiniErp.Infrastructure.Services.FiscalYears;

public sealed partial class FiscalYearService
{
    private async Task<Result> TransferClosingBalancesAsync(
        FiscalYear fiscalYear,
        FiscalYear nextYear,
        CancellationToken cancellationToken)
    {
        var existingTransfer = await dbContext.JournalEntries
            .Include(entry => entry.Lines)
            .SingleOrDefaultAsync(entry =>
                entry.CompanyId == companyId &&
                entry.EntryType == JournalEntryType.Opening &&
                entry.SourceType == JournalEntrySourceType.FiscalYearClosing &&
                entry.SourceId == fiscalYear.Id,
                cancellationToken);

        var balances = await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.JournalEntry.FiscalYearId == fiscalYear.Id &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null &&
                line.JournalEntry.ReversedOn == null &&
                line.Account.AccountType != AccountType.Revenue &&
                line.Account.AccountType != AccountType.Expense)
            .GroupBy(line => new
            {
                line.AccountId,
                line.Account.Code,
                line.Account.Name,
                line.PartyType,
                line.PartyId,
                line.Currency
            })
            .Select(group => new
            {
                group.Key.AccountId,
                group.Key.Code,
                group.Key.Name,
                group.Key.PartyType,
                group.Key.PartyId,
                group.Key.Currency,
                Balance = group.Sum(line => line.Debit - line.Credit),
                TransactionBalance = group.Sum(line =>
                    line.TransactionDebit - line.TransactionCredit),
                ReferenceLineId = group.Max(line => line.Id)
            })
            .Where(row => row.Balance != 0m || row.TransactionBalance != 0m)
            .ToListAsync(cancellationToken);

        var carriedDrivers = balances
            .Where(row => row.PartyType == JournalPartyType.Driver && row.PartyId > 0)
            .GroupBy(row => row.PartyId!.Value)
            .Where(group => group.Sum(row => row.Balance) != 0m)
            .Select(group => group.Key)
            .ToArray();
        if (carriedDrivers.Length > 0 &&
            (await dbContext.DriverOpeningBalances.AnyAsync(row =>
                row.CompanyId == companyId && row.FiscalYearId == nextYear.Id &&
                carriedDrivers.Contains(row.DriverId), cancellationToken) ||
             await DriverOpeningBalanceLedgerQueries.LegacyOpeningLines(
                    dbContext, companyId, nextYear.Id, nextYear.StartDate)
                .Where(line => carriedDrivers.Contains(line.PartyId!.Value))
                .GroupBy(line => line.PartyId!.Value)
                .AnyAsync(group => group.Sum(line => line.Debit - line.Credit) != 0m, cancellationToken)))
        {
            return Result.Failure(DriverOpeningBalanceConflict(nextYear.Name));
        }

        var employeeDeltas = await LoadEmployeeOperationalDeltasAsync(
            fiscalYear.Id,
            cancellationToken);

        if (balances.Count == 0 && employeeDeltas.Count == 0)
        {
            if (existingTransfer is not null)
            {
                dbContext.JournalEntryLines.RemoveRange(existingTransfer.Lines);
                dbContext.JournalEntries.Remove(existingTransfer);
            }
            return Result.Success();
        }

        var baseCurrency = await dbContext.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

        var lines = new List<JournalEntryLine>();
        var baseBalances = balances
            .Where(balance => balance.Currency == baseCurrency)
            .ToDictionary(
                balance => (balance.AccountId, balance.PartyType, balance.PartyId),
                balance => balance.Balance);
        var referenceLineIds = balances
            .Where(balance =>
                balance.Currency != baseCurrency &&
                balance.TransactionBalance != 0m &&
                (Math.Sign(balance.Balance) != Math.Sign(balance.TransactionBalance) ||
                 !ExchangeRateRules.IsValidRate(ExchangeRateRules.RoundRate(
                     Math.Abs(balance.Balance / balance.TransactionBalance)))))
            .Select(balance => balance.ReferenceLineId)
            .ToArray();
        var referenceRates = referenceLineIds.Length == 0
            ? new Dictionary<int, decimal>()
            : await dbContext.JournalEntryLines
                .AsNoTracking()
                .Where(line => line.CompanyId == companyId && referenceLineIds.Contains(line.Id))
                .ToDictionaryAsync(line => line.Id, line => line.ExchangeRate, cancellationToken);

        foreach (var balance in balances.Where(balance => balance.Currency != baseCurrency))
        {
            var key = (balance.AccountId, balance.PartyType, balance.PartyId);
            if (balance.TransactionBalance == 0m)
            {
                // A valuation residual has no foreign units to carry forward.
                baseBalances.TryGetValue(key, out var existingBaseBalance);
                baseBalances[key] = existingBaseBalance + balance.Balance;
                continue;
            }

            var rate = ExchangeRateRules.RoundRate(
                Math.Abs(balance.Balance / balance.TransactionBalance));
            var nativeLineBaseBalance = balance.Balance;
            if (Math.Sign(balance.Balance) != Math.Sign(balance.TransactionBalance) ||
                !ExchangeRateRules.IsValidRate(rate))
            {
                // Journal lines require positive base/native amounts on the same
                // side. Use a historical rate for the units and offset its base
                // value in the base-currency group to retain the actual ledger balance.
                rate = referenceRates[balance.ReferenceLineId];
                var referenceBaseAmount = Math.Max(
                    0.0001m,
                    decimal.Round(
                        Math.Abs(balance.TransactionBalance) * rate,
                        4,
                        MidpointRounding.AwayFromZero));
                nativeLineBaseBalance = Math.Sign(balance.TransactionBalance) *
                    referenceBaseAmount;
                baseBalances.TryGetValue(key, out var existingBaseBalance);
                baseBalances[key] = existingBaseBalance + balance.Balance -
                    nativeLineBaseBalance;
            }

            lines.Add(new JournalEntryLine
            {
                CompanyId = companyId,
                AccountId = balance.AccountId,
                PartyType = balance.PartyType,
                PartyId = balance.PartyId,
                Description = $"ترحيل رصيد {balance.Code} - {balance.Name}",
                Debit = nativeLineBaseBalance > 0m ? nativeLineBaseBalance : 0m,
                Credit = nativeLineBaseBalance < 0m ? -nativeLineBaseBalance : 0m,
                Currency = balance.Currency,
                ExchangeRate = rate,
                TransactionDebit = balance.TransactionBalance > 0m
                    ? balance.TransactionBalance
                    : 0m,
                TransactionCredit = balance.TransactionBalance < 0m
                    ? -balance.TransactionBalance
                    : 0m
            });
        }

        foreach (var (key, balance) in baseBalances.Where(row => row.Value != 0m))
        {
            lines.Add(new JournalEntryLine
            {
                CompanyId = companyId,
                AccountId = key.AccountId,
                PartyType = key.PartyType,
                PartyId = key.PartyId,
                Description = "ترحيل الرصيد بعملة الأساس",
                Debit = balance > 0m ? balance : 0m,
                Credit = balance < 0m ? -balance : 0m,
                Currency = baseCurrency,
                ExchangeRate = 1m,
                TransactionDebit = balance > 0m ? balance : 0m,
                TransactionCredit = balance < 0m ? -balance : 0m
            });
        }

        if (employeeDeltas.Count > 0)
        {
            var requiredMappingTypes = employeeDeltas
                .Select(delta => delta.BaseBalance > 0m
                    ? AccountingMappingType.EmployeeReceivable
                    : AccountingMappingType.EmployeeControl)
                .Distinct()
                .ToArray();
            var employeeAccounts = await dbContext.AccountMappings
                .AsNoTracking()
                .Where(mapping =>
                    mapping.CompanyId == companyId &&
                    mapping.FiscalYearId == nextYear.Id &&
                    mapping.SourceId == null &&
                    requiredMappingTypes.Contains(mapping.MappingType))
                .ToDictionaryAsync(
                    mapping => mapping.MappingType,
                    mapping => mapping.AccountId,
                    cancellationToken);

            foreach (var delta in employeeDeltas)
            {
                var mappingType = delta.BaseBalance > 0m
                    ? AccountingMappingType.EmployeeReceivable
                    : AccountingMappingType.EmployeeControl;
                if (!employeeAccounts.TryGetValue(mappingType, out var accountId))
                {
                    return Result.Failure(
                        EmployeeBalanceAccountMissing(nextYear.Name, mappingType));
                }

                var transactionBalance = delta.TransactionBalance == 0m
                    ? delta.BaseBalance
                    : delta.TransactionBalance;
                lines.Add(new JournalEntryLine
                {
                    CompanyId = companyId,
                    AccountId = accountId,
                    PartyType = JournalPartyType.Employee,
                    PartyId = delta.EmployeeId,
                    Description = "ترحيل فرق الرصيد التشغيلي للموظف",
                    Debit = delta.BaseBalance > 0m ? delta.BaseBalance : 0m,
                    Credit = delta.BaseBalance < 0m ? -delta.BaseBalance : 0m,
                    Currency = delta.Currency,
                    ExchangeRate = delta.Currency == baseCurrency
                        ? 1m
                        : ExchangeRateRules.RoundRate(
                            Math.Abs(delta.BaseBalance / transactionBalance)),
                    TransactionDebit = transactionBalance > 0m
                        ? transactionBalance
                        : 0m,
                    TransactionCredit = transactionBalance < 0m
                        ? -transactionBalance
                        : 0m
                });
            }
        }
        if (lines.Count == 0)
        {
            if (existingTransfer is not null)
            {
                dbContext.JournalEntryLines.RemoveRange(existingTransfer.Lines);
                dbContext.JournalEntries.Remove(existingTransfer);
            }
            return Result.Success();
        }

        var net = lines.Sum(line => line.Debit - line.Credit);
        if (net != 0m)
        {
            var equityAccountId = await dbContext.AccountMappings
                .AsNoTracking()
                .Where(mapping =>
                    mapping.CompanyId == companyId &&
                    mapping.FiscalYearId == nextYear.Id &&
                    mapping.MappingType == AccountingMappingType.OpeningBalanceEquity &&
                    mapping.SourceId == null)
                .Select(mapping => (int?)mapping.AccountId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!equityAccountId.HasValue)
            {
                return Result.Failure(
                    OpeningBalanceAccountMissing(nextYear.Name));
            }

            lines.Add(new JournalEntryLine
            {
                CompanyId = companyId,
                AccountId = equityAccountId.Value,
                Description = "مقابل ترحيل أرصدة المركز المالي",
                Debit = net < 0m ? -net : 0m,
                Credit = net > 0m ? net : 0m,
                Currency = baseCurrency,
                ExchangeRate = 1m,
                TransactionDebit = net < 0m ? -net : 0m,
                TransactionCredit = net > 0m ? net : 0m
            });
        }

        if (existingTransfer is not null)
        {
            dbContext.JournalEntryLines.RemoveRange(existingTransfer.Lines);
            existingTransfer.FiscalYearId = nextYear.Id;
            existingTransfer.EntryDate = nextYear.StartDate;
            existingTransfer.Description = $"أرصدة افتتاحية مرحّلة من السنة المالية {fiscalYear.Name}";
            existingTransfer.SourceNumber = fiscalYear.Name;
            existingTransfer.PostedOn = timeProvider.GetUtcNow().UtcDateTime;
            existingTransfer.Lines = lines;
            return Result.Success();
        }

        var entryNumber = await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "OB",
            companyId,
            dbContext.JournalEntries
                .IgnoreQueryFilters()
                .Where(entry => entry.CompanyId == companyId)
                .Select(entry => entry.EntryNumber),
            cancellationToken);
        dbContext.JournalEntries.Add(new JournalEntry
        {
            CompanyId = companyId,
            FiscalYearId = nextYear.Id,
            EntryNumber = entryNumber,
            EntryDate = nextYear.StartDate,
            Description = $"أرصدة افتتاحية مرحّلة من السنة المالية {fiscalYear.Name}",
            EntryType = JournalEntryType.Opening,
            SourceType = JournalEntrySourceType.FiscalYearClosing,
            SourceId = fiscalYear.Id,
            SourceNumber = fiscalYear.Name,
            Status = JournalEntryStatus.Posted,
            PostedOn = timeProvider.GetUtcNow().UtcDateTime,
            Lines = lines
        });
        return Result.Success();
    }

    private async Task<IReadOnlyList<EmployeeOperationalDelta>>
        LoadEmployeeOperationalDeltasAsync(
            int fiscalYearId,
            CancellationToken cancellationToken)
    {
        var openingBalances = await dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == fiscalYearId)
            .Select(balance => new EmployeeOperationalBalance(
                balance.EmployeeId,
                balance.Currency,
                balance.BalanceType == EmployeeBalanceType.Debit
                    ? (balance.BaseAmount != 0m
                        ? balance.BaseAmount
                        : balance.Amount)
                    : -(balance.BaseAmount != 0m
                        ? balance.BaseAmount
                        : balance.Amount),
                balance.BalanceType == EmployeeBalanceType.Debit
                    ? balance.Amount
                    : -balance.Amount))
            .ToListAsync(cancellationToken);

        var movements = await dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.FiscalYearId == fiscalYearId)
            .Select(movement => new EmployeeOperationalBalance(
                movement.EmployeeId,
                movement.Currency,
                movement.BaseDebit - movement.BaseCredit,
                movement.Debit - movement.Credit))
            .ToListAsync(cancellationToken);

        var manualLedger = await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.PartyType == JournalPartyType.Employee &&
                line.PartyId.HasValue &&
                line.JournalEntry.FiscalYearId == fiscalYearId &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null &&
                line.JournalEntry.ReversedOn == null &&
                line.JournalEntry.SourceType !=
                    JournalEntrySourceType.EmployeeOpeningBalance &&
                line.JournalEntry.SourceType !=
                    JournalEntrySourceType.CashVoucher)
            .Select(line => new EmployeeOperationalBalance(
                line.PartyId!.Value,
                line.Currency,
                line.Debit - line.Credit,
                line.TransactionDebit - line.TransactionCredit))
            .ToListAsync(cancellationToken);

        var postedLedger = await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.PartyType == JournalPartyType.Employee &&
                line.PartyId.HasValue &&
                line.JournalEntry.FiscalYearId == fiscalYearId &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null &&
                line.JournalEntry.ReversedOn == null &&
                line.Account.AccountType != AccountType.Revenue &&
                line.Account.AccountType != AccountType.Expense)
            .Select(line => new EmployeeOperationalBalance(
                line.PartyId!.Value,
                line.Currency,
                line.Debit - line.Credit,
                line.TransactionDebit - line.TransactionCredit))
            .ToListAsync(cancellationToken);

        var operational = openingBalances
            .Concat(movements)
            .Concat(manualLedger)
            .GroupBy(row => new { row.EmployeeId, row.Currency })
            .ToDictionary(
                group => (group.Key.EmployeeId, group.Key.Currency),
                group => (
                    Base: group.Sum(row => row.BaseBalance),
                    Transaction: group.Sum(row => row.TransactionBalance)));
        var ledger = postedLedger
            .GroupBy(row => new { row.EmployeeId, row.Currency })
            .ToDictionary(
                group => (group.Key.EmployeeId, group.Key.Currency),
                group => (
                    Base: group.Sum(row => row.BaseBalance),
                    Transaction: group.Sum(row => row.TransactionBalance)));

        return operational.Keys
            .Union(ledger.Keys)
            .Select(key =>
            {
                operational.TryGetValue(key, out var operationalBalance);
                ledger.TryGetValue(key, out var ledgerBalance);
                return new EmployeeOperationalDelta(
                    EmployeeId: key.EmployeeId,
                    Currency: key.Currency,
                    BaseBalance: operationalBalance.Base - ledgerBalance.Base,
                    TransactionBalance: operationalBalance.Transaction -
                        ledgerBalance.Transaction);
            })
            .Where(delta => delta.BaseBalance != 0m)
            .ToArray();
    }

    private sealed record EmployeeOperationalBalance(
        int EmployeeId,
        CurrencyCode Currency,
        decimal BaseBalance,
        decimal TransactionBalance);

    private sealed record EmployeeOperationalDelta(
        int EmployeeId,
        CurrencyCode Currency,
        decimal BaseBalance,
        decimal TransactionBalance);

    private async Task EnsureExchangeRateCarryForwardAsync(
        FiscalYear fiscalYear,
        CancellationToken cancellationToken)
    {
        // Documents resolve rates inside their own fiscal year only, so every
        // currency needs a rate on the year's first day. It runs when the
        // year is created and again when the previous year is closed: rates
        // entered after creation (for example on 31 December) must still
        // reach the start date. A user-entered start-date rate is kept; a
        // carried-forward one is refreshed while no document uses it.
        var latestRates = (await dbContext.ExchangeRates
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(rate =>
                    rate.CompanyId == companyId &&
                    !rate.IsDeleted &&
                    rate.RateDate < fiscalYear.StartDate)
                .OrderByDescending(rate => rate.RateDate)
                .ThenByDescending(rate => rate.Id)
                .ToListAsync(cancellationToken))
            .GroupBy(rate => rate.Currency)
            .Select(group => group.First())
            .ToList();
        if (latestRates.Count == 0)
        {
            return;
        }

        var startDateRates = await dbContext.ExchangeRates
            .IgnoreQueryFilters()
            .Where(rate =>
                rate.CompanyId == companyId &&
                !rate.IsDeleted &&
                rate.FiscalYearId == fiscalYear.Id &&
                rate.RateDate == fiscalYear.StartDate)
            .ToDictionaryAsync(rate => rate.Currency, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var changed = false;

        foreach (var latestRate in latestRates)
        {
            var notes = string.Create(
                CultureInfo.InvariantCulture,
                $"مرحل تلقائيًا من سعر {latestRate.RateDate:yyyy-MM-dd}");
            if (!startDateRates.TryGetValue(
                    latestRate.Currency,
                    out var startDateRate))
            {
                var carryForward = new ExchangeRate
                {
                    CompanyId = companyId,
                    FiscalYearId = fiscalYear.Id,
                    Currency = latestRate.Currency,
                    RateDate = fiscalYear.StartDate,
                    Rate = latestRate.Rate,
                    Source = ExchangeRateSource.CarriedForward,
                    Provider = latestRate.Provider,
                    Notes = notes
                };
                carryForward.Touch(now);
                dbContext.ExchangeRates.Add(carryForward);
                changed = true;
                continue;
            }

            if (startDateRate.Source != ExchangeRateSource.CarriedForward ||
                (startDateRate.Rate == latestRate.Rate &&
                 startDateRate.Notes == notes) ||
                await ExchangeRateReferences.IsReferencedAsync(
                    dbContext,
                    companyId,
                    startDateRate.Id,
                    cancellationToken))
            {
                continue;
            }

            startDateRate.Rate = latestRate.Rate;
            startDateRate.Provider = latestRate.Provider;
            startDateRate.Notes = notes;
            startDateRate.Touch(now);
            dbContext.Entry(startDateRate)
                .Property(rate => rate.LastModifiedAt)
                .IsModified = true;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private IQueryable<FiscalYearResponse> ProjectResponseQuery(
        int? id = null,
        bool? isCurrent = null) =>
        dbContext.FiscalYears
            .AsNoTracking()
            .Where(fiscalYear =>
                fiscalYear.CompanyId == companyId &&
                (!id.HasValue || fiscalYear.Id == id.Value) &&
                (!isCurrent.HasValue ||
                 fiscalYear.IsCurrent == isCurrent.Value))
            .ProjectToType<FiscalYearResponse>();

    private Task<bool> NameExistsAsync(
        string name,
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.FiscalYears
            .AsNoTracking()
            .AnyAsync(
                fiscalYear =>
                    fiscalYear.CompanyId == companyId &&
                    (!excludedId.HasValue ||
                     fiscalYear.Id != excludedId.Value) &&
                    fiscalYear.Name.ToUpper() == name.Trim().ToUpper(),
                cancellationToken);

    private async Task<string> GenerateNextFiscalYearNameAsync(
        FiscalYear fiscalYear,
        CancellationToken cancellationToken)
    {
        var nextStartYear = fiscalYear.StartDate.AddYears(1).Year;
        var baseName = fiscalYear.Name.Trim() ==
            fiscalYear.StartDate.Year.ToString()
                ? nextStartYear.ToString()
                : $"{fiscalYear.Name.Trim()} - {nextStartYear}";
        var candidate = baseName;
        var suffix = 2;
        while (await NameExistsAsync(
                   candidate,
                   excludedId: null,
                   cancellationToken))
        {
            candidate = $"{baseName} ({suffix++})";
        }

        return candidate;
    }

    private Task<bool> DateRangeOverlapsAsync(
        DateOnly startDate,
        DateOnly endDate,
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.FiscalYears
            .AsNoTracking()
            .AnyAsync(
                fiscalYear =>
                    fiscalYear.CompanyId == companyId &&
                    (!excludedId.HasValue ||
                     fiscalYear.Id != excludedId.Value) &&
                    fiscalYear.StartDate <= endDate &&
                    fiscalYear.EndDate >= startDate,
                cancellationToken);

    private Task<int> ClearCurrentAsync(
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.FiscalYears
            .Where(fiscalYear =>
                fiscalYear.CompanyId == companyId &&
                fiscalYear.IsCurrent &&
                (!excludedId.HasValue ||
                 fiscalYear.Id != excludedId.Value))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    fiscalYear => fiscalYear.IsCurrent,
                    false),
                cancellationToken);
}
