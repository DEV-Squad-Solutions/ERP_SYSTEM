using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.CashboxRevaluations;
using MiniErp.Infrastructure.Services.DriverOpeningBalances;
using static MiniErp.Application.Features.JournalEntries.JournalEntryErrors;

namespace MiniErp.Infrastructure.Services.JournalEntries;

public sealed partial class JournalEntryService
{
    private async Task<bool> HasDriverOpeningConflictAsync(
        int fiscalYearId, DateOnly entryDate, IReadOnlyList<JournalEntryLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var driverIds = lines.Where(line => line.PartyType == JournalPartyType.Driver && line.PartyId > 0)
            .GroupBy(line => line.PartyId!.Value)
            .Where(group => group.Sum(line => line.Debit - line.Credit) != 0m)
            .Select(group => group.Key)
            .ToArray();
        if (driverIds.Length == 0 || !await dbContext.FiscalYears.AsNoTracking().AnyAsync(
            year => year.CompanyId == companyId && year.Id == fiscalYearId && year.StartDate == entryDate,
            cancellationToken))
        {
            return false;
        }

        return await dbContext.DriverOpeningBalances.AnyAsync(row =>
                   row.CompanyId == companyId && row.FiscalYearId == fiscalYearId &&
                   driverIds.Contains(row.DriverId), cancellationToken) ||
               await DriverOpeningBalanceLedgerQueries.CarriedLines(dbContext, companyId, fiscalYearId)
                   .Where(line => driverIds.Contains(line.PartyId!.Value))
                   .GroupBy(line => line.PartyId!.Value)
                   .AnyAsync(group => group.Sum(line => line.Debit - line.Credit) != 0m, cancellationToken);
    }

    private async Task<Result> ValidateFiscalYearAsync(
        int fiscalYearId,
        DateOnly entryDate,
        CancellationToken cancellationToken)
    {
        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.Id == fiscalYearId)
            .Select(year => new
            {
                year.StartDate,
                year.EndDate,
                year.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result.Failure(FiscalYearNotFound(fiscalYearId));
        }

        if (entryDate < fiscalYear.StartDate || entryDate > fiscalYear.EndDate)
        {
            return Result.Failure(EntryDateOutsideFiscalYear());
        }

        return fiscalYear.Status == FiscalYearStatus.Open
            ? Result.Success()
            : Result.Failure(FiscalYearClosed());
    }

    private async Task<Result> ValidateAccountsAsync(
        IReadOnlyList<JournalEntryLineRequest> lines,
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        var accountIds = lines
            .Select(line => line.AccountId)
            .Distinct()
            .ToArray();
        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                accountIds.Contains(account.Id))
            .Select(account => new
            {
                account.Id,
                account.IsActive,
                account.IsPosting,
                account.ParentAccountId
            })
            .ToDictionaryAsync(account => account.Id, cancellationToken);
        var mappingRows = await dbContext.AccountMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId &&
                accountIds.Contains(mapping.AccountId) &&
                (mapping.MappingType == AccountingMappingType.CustomerControl ||
                 mapping.MappingType == AccountingMappingType.SupplierControl ||
                 mapping.MappingType == AccountingMappingType.EmployeeControl ||
                 mapping.MappingType == AccountingMappingType.EmployeeReceivable ||
                 mapping.MappingType == AccountingMappingType.DriverControl ||
                 mapping.MappingType == AccountingMappingType.Cashbox))
            .Select(mapping => new
            {
                mapping.AccountId,
                mapping.MappingType,
                mapping.SourceId
            })
            .ToListAsync(cancellationToken);
        var partyTypesByAccount = mappingRows
            .Select(mapping => new
            {
                mapping.AccountId,
                PartyType = ToJournalPartyType(mapping.MappingType)
            })
            .Distinct()
            .ToLookup(mapping => mapping.AccountId, mapping => mapping.PartyType);
        var cashboxIdsByAccount = mappingRows
            .Where(mapping =>
                mapping.MappingType == AccountingMappingType.Cashbox &&
                mapping.SourceId.HasValue)
            .ToLookup(mapping => mapping.AccountId, mapping => mapping.SourceId!.Value);
        var partnerIds = lines
            .Where(line => line.PartyType is
                JournalPartyType.Customer or JournalPartyType.Supplier)
            .Select(line => line.PartyId)
            .OfType<int>()
            .Distinct()
            .ToArray();
        var partners = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(party =>
                party.CompanyId == companyId &&
                partnerIds.Contains(party.Id))
            .Select(party => new
            {
                party.Id,
                party.IsActive,
                party.Currency
            })
            .ToDictionaryAsync(party => party.Id, cancellationToken);
        var employeeIds = lines
            .Where(line => line.PartyType == JournalPartyType.Employee)
            .Select(line => line.PartyId)
            .OfType<int>()
            .Distinct()
            .ToArray();
        var employees = await dbContext.Employees
            .AsNoTracking()
            .Where(party =>
                party.CompanyId == companyId &&
                employeeIds.Contains(party.Id))
            .Select(party => new { party.Id, party.IsActive })
            .ToDictionaryAsync(party => party.Id, cancellationToken);
        var driverIds = lines
            .Where(line => line.PartyType == JournalPartyType.Driver)
            .Select(line => line.PartyId)
            .OfType<int>()
            .Distinct()
            .ToArray();
        var drivers = await dbContext.Drivers
            .AsNoTracking()
            .Where(party =>
                party.CompanyId == companyId &&
                driverIds.Contains(party.Id))
            .Select(party => new { party.Id, party.IsActive })
            .ToDictionaryAsync(party => party.Id, cancellationToken);
        var cashboxIds = lines
            .Where(line => line.PartyType == JournalPartyType.Cashbox)
            .Select(line => line.PartyId)
            .OfType<int>()
            .Distinct()
            .ToArray();
        if (cashboxAccessService is not null)
        {
            foreach (var cashboxId in cashboxIds)
            {
                var access = await cashboxAccessService.EnsureExecuteAsync(cashboxId, cancellationToken);
                if (access.IsFailure) return access;
            }
        }
        var cashboxes = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashboxIds.Contains(cashbox.Id))
            .Select(cashbox => new
            {
                cashbox.Id,
                cashbox.IsActive,
                cashbox.Currency
            })
            .ToDictionaryAsync(cashbox => cashbox.Id, cancellationToken);
        var errors = new List<Error>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var accountId = line.AccountId;
            if (!accounts.TryGetValue(accountId, out var account))
            {
                errors.Add(AccountNotFound(accountId, index));
            }
            else if (!account.IsActive)
            {
                errors.Add(AccountInactive(accountId, index));
            }
            else if (!account.IsPosting)
            {
                errors.Add(AccountNotPosting(accountId, index));
            }
            else if (!account.ParentAccountId.HasValue)
            {
                errors.Add(AccountMustBeChild(accountId, index));
            }

            var allowedPartyTypes = partyTypesByAccount[accountId].ToHashSet();
            if (line.PartyType.HasValue != line.PartyId.HasValue)
            {
                errors.Add(PartyShapeInvalid(index));
                continue;
            }

            if (allowedPartyTypes.Count == 0)
            {
                if (line.PartyType.HasValue)
                {
                    errors.Add(PartyNotAllowed(accountId, index));
                }

                continue;
            }

            if (!line.PartyType.HasValue || !line.PartyId.HasValue)
            {
                errors.Add(PartyRequired(accountId, index));
                continue;
            }

            if (!Enum.IsDefined(line.PartyType.Value) ||
                !allowedPartyTypes.Contains(line.PartyType.Value))
            {
                errors.Add(PartyTypeNotAllowed(accountId, index));
                continue;
            }

            if (line.PartyType == JournalPartyType.Cashbox &&
                !cashboxIdsByAccount[accountId].Contains(line.PartyId.Value))
            {
                errors.Add(PartyNotAllowed(accountId, index));
                continue;
            }

            var partyState = line.PartyType.Value switch
            {
                JournalPartyType.Customer or JournalPartyType.Supplier =>
                    partners.TryGetValue(line.PartyId.Value, out var party)
                        ? (Found: true, party.IsActive,
                            Currency: (CurrencyCode?)party.Currency)
                        : (Found: false, IsActive: false,
                            Currency: (CurrencyCode?)null),
                JournalPartyType.Employee =>
                    employees.TryGetValue(line.PartyId.Value, out var party)
                        ? (Found: true, party.IsActive,
                            Currency: (CurrencyCode?)null)
                        : (Found: false, IsActive: false,
                            Currency: (CurrencyCode?)null),
                JournalPartyType.Driver =>
                    drivers.TryGetValue(line.PartyId.Value, out var party)
                        ? (Found: true, party.IsActive,
                            Currency: (CurrencyCode?)null)
                        : (Found: false, IsActive: false,
                            Currency: (CurrencyCode?)null),
                JournalPartyType.Cashbox =>
                    cashboxes.TryGetValue(line.PartyId.Value, out var party)
                        ? (Found: true, party.IsActive,
                            Currency: (CurrencyCode?)party.Currency)
                        : (Found: false, IsActive: false,
                            Currency: (CurrencyCode?)null),
                _ => (Found: false, IsActive: false,
                    Currency: (CurrencyCode?)null)
            };
            if (!partyState.Found)
            {
                errors.Add(PartyNotFound(line.PartyId.Value, index));
            }
            else if (!partyState.IsActive)
            {
                errors.Add(PartyInactive(line.PartyId.Value, index));
            }
            else if (partyState.Currency.HasValue &&
                     line.Currency != partyState.Currency.Value)
            {
                errors.Add(PartyCurrencyMismatch(
                    partyState.Currency.Value,
                    index));
            }
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(errors);
    }

    private static JournalPartyType ToJournalPartyType(
        AccountingMappingType mappingType) => mappingType switch
        {
            AccountingMappingType.CustomerControl => JournalPartyType.Customer,
            AccountingMappingType.SupplierControl => JournalPartyType.Supplier,
            AccountingMappingType.EmployeeControl or
            AccountingMappingType.EmployeeReceivable => JournalPartyType.Employee,
            AccountingMappingType.DriverControl => JournalPartyType.Driver,
            AccountingMappingType.Cashbox => JournalPartyType.Cashbox,
            _ => throw new ArgumentOutOfRangeException(nameof(mappingType))
        };

    private static Result ValidateBalance(
        IReadOnlyList<JournalEntryLineRequest> lines)
    {
        var totalDebit = lines.Sum(line => line.Debit);
        var totalCredit = lines.Sum(line => line.Credit);
        return totalDebit > 0m && totalDebit == totalCredit
            ? Result.Success()
            : Result.Failure(Unbalanced());
    }

    private async Task<IReadOnlyList<JournalEntryResponse>> LoadResponsesAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken,
        int? fiscalYearId = null)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var entries = await dbContext.JournalEntries
            .AsNoTracking()
            .AsSplitQuery()
            .Include(entry => entry.FiscalYear)
            .Include(entry => entry.Lines)
                .ThenInclude(line => line.Account)
            .Where(entry =>
                entry.CompanyId == companyId &&
                ids.Contains(entry.Id) &&
                (!fiscalYearId.HasValue ||
                 entry.FiscalYearId == fiscalYearId.Value))
            .ToListAsync(cancellationToken);

        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);

        var partnerIds = entries
            .SelectMany(entry => entry.Lines)
            .Where(line => line.PartyType is
                JournalPartyType.Customer or JournalPartyType.Supplier)
            .Select(line => line.PartyId)
            .OfType<int>()
            .Distinct()
            .ToArray();
        var partners = await dbContext.BusinessPartners
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(party =>
                party.CompanyId == companyId &&
                partnerIds.Contains(party.Id))
            .Select(party => new { party.Id, party.Code, party.Name })
            .ToDictionaryAsync(
                party => party.Id,
                party => new PartySnapshot(party.Code, party.Name),
                cancellationToken);
        var employeeIds = entries
            .SelectMany(entry => entry.Lines)
            .Where(line => line.PartyType == JournalPartyType.Employee)
            .Select(line => line.PartyId)
            .OfType<int>()
            .Distinct()
            .ToArray();
        var employees = await dbContext.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(party =>
                party.CompanyId == companyId &&
                employeeIds.Contains(party.Id))
            .Select(party => new { party.Id, party.Code, party.Name })
            .ToDictionaryAsync(
                party => party.Id,
                party => new PartySnapshot(party.Code, party.Name),
                cancellationToken);
        var driverIds = entries
            .SelectMany(entry => entry.Lines)
            .Where(line => line.PartyType == JournalPartyType.Driver)
            .Select(line => line.PartyId)
            .OfType<int>()
            .Distinct()
            .ToArray();
        var drivers = await dbContext.Drivers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(party =>
                party.CompanyId == companyId &&
                driverIds.Contains(party.Id))
            .Select(party => new { party.Id, party.Code, party.Name })
            .ToDictionaryAsync(
                party => party.Id,
                party => new PartySnapshot(party.Code, party.Name),
                cancellationToken);
        var cashboxIds = entries
            .SelectMany(entry => entry.Lines)
            .Where(line => line.PartyType == JournalPartyType.Cashbox)
            .Select(line => line.PartyId)
            .OfType<int>()
            .Distinct()
            .ToArray();
        var cashboxes = await dbContext.Cashboxes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashboxIds.Contains(cashbox.Id))
            .Select(cashbox => new { cashbox.Id, cashbox.Code, cashbox.Name })
            .ToDictionaryAsync(
                cashbox => cashbox.Id,
                cashbox => new PartySnapshot(cashbox.Code, cashbox.Name),
                cancellationToken);

        var relatedIds = entries
            .Where(entry => entry.ReversalOfEntryId.HasValue)
            .Select(entry => entry.ReversalOfEntryId!.Value)
            .Distinct()
            .ToArray();
        var relatedNumbers = relatedIds.Length == 0
            ? new Dictionary<int, string>()
            : await dbContext.JournalEntries
                .AsNoTracking()
                .Where(entry =>
                    entry.CompanyId == companyId &&
                    relatedIds.Contains(entry.Id))
                .ToDictionaryAsync(
                    entry => entry.Id,
                    entry => entry.EntryNumber,
                    cancellationToken);

        var reversals = await dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry =>
                entry.CompanyId == companyId &&
                entry.ReversalOfEntryId.HasValue &&
                ids.Contains(entry.ReversalOfEntryId.Value))
            .Select(entry => new
            {
                OriginalId = entry.ReversalOfEntryId!.Value,
                entry.Id,
                entry.EntryNumber
            })
            .ToDictionaryAsync(entry => entry.OriginalId, cancellationToken);

        return entries
            .Select(entry =>
            {
                reversals.TryGetValue(entry.Id, out var reversedBy);
                var lines = entry.Lines
                    .OrderBy(line => line.Id)
                    .Select(line =>
                    {
                        var party = ResolveParty(
                            line.PartyType,
                            line.PartyId,
                            partners,
                            employees,
                            drivers,
                            cashboxes);
                        return new JournalEntryLineResponse(
                            Id: line.Id,
                            AccountId: line.AccountId,
                            AccountCode: line.Account.Code,
                            AccountName: line.Account.Name,
                            Description: line.Description,
                            Debit: line.Debit,
                            Credit: line.Credit,
                            Currency: line.Currency,
                            ExchangeRate: line.ExchangeRate,
                            TransactionDebit: line.TransactionDebit,
                            TransactionCredit: line.TransactionCredit,
                            PartyType: line.PartyType,
                            PartyId: line.PartyId,
                            PartyCode: party?.Code,
                            PartyName: party?.Name);
                    })
                    .ToArray();
                return new JournalEntryResponse(
                    Id: entry.Id,
                    CompanyId: entry.CompanyId,
                    FiscalYearId: entry.FiscalYearId,
                    FiscalYearName: entry.FiscalYear.Name,
                    EntryNumber: entry.EntryNumber,
                    EntryDate: entry.EntryDate,
                    Description: entry.Description,
                    EntryType: entry.EntryType,
                    SourceType: entry.SourceType,
                    SourceId: entry.SourceId,
                    SourceNumber: entry.SourceNumber,
                    Status: entry.Status,
                    BaseCurrency: baseCurrency,
                    TotalDebit: lines.Sum(line => line.Debit),
                    TotalCredit: lines.Sum(line => line.Credit),
                    PostedOn: entry.PostedOn,
                    ReversedOn: entry.ReversedOn,
                    ReversalOfEntryId: entry.ReversalOfEntryId,
                    ReversalOfEntryNumber: entry.ReversalOfEntryId.HasValue &&
                        relatedNumbers.TryGetValue(
                            entry.ReversalOfEntryId.Value,
                            out var originalNumber)
                            ? originalNumber
                            : null,
                    ReversedByEntryId: reversedBy?.Id,
                    ReversedByEntryNumber: reversedBy?.EntryNumber,
                    CreatedById: entry.CreatedById,
                    CreatedOn: entry.CreatedOn,
                    RowVersion: entry.RowVersion,
                    Lines: lines);
            })
            .ToArray();
    }

    private async Task<Result> EnsureEntryCashboxesAsync(int id, bool execute, CancellationToken cancellationToken)
    {
        if (cashboxAccessService is null) return Result.Success();
        var hasUnresolvedCashboxLine = await dbContext.JournalEntryLines.AsNoTracking()
            .AnyAsync(line => line.CompanyId == companyId && line.JournalEntryId == id &&
                ((line.PartyType == JournalPartyType.Cashbox && !line.PartyId.HasValue) ||
                 (line.PartyType != JournalPartyType.Cashbox && dbContext.AccountMappings.Any(mapping =>
                     mapping.CompanyId == companyId && mapping.FiscalYearId == line.JournalEntry.FiscalYearId &&
                     mapping.MappingType == AccountingMappingType.Cashbox && mapping.AccountId == line.AccountId))), cancellationToken);
        if (hasUnresolvedCashboxLine)
        {
            var scope = await cashboxAccessService.GetScopeAsync(cancellationToken);
            if (scope.IsFailure) return Result.Failure(scope.Errors);
            if (!scope.Value.HasAllCashboxes)
                return Result.Failure(Error.Forbidden("CashboxPermissions.FullCompanyRequired",
                    "Legacy cashbox account lines without an identifiable cashbox require full-company access."));
        }
        var cashboxIds = await dbContext.JournalEntryLines.AsNoTracking()
            .Where(line => line.CompanyId == companyId && line.JournalEntryId == id &&
                line.PartyType == JournalPartyType.Cashbox && line.PartyId.HasValue)
            .Select(line => line.PartyId!.Value).Distinct().ToArrayAsync(cancellationToken);
        foreach (var cashboxId in cashboxIds)
        {
            var access = execute
                ? await cashboxAccessService.EnsureExecuteAsync(cashboxId, cancellationToken)
                : await cashboxAccessService.EnsureViewAsync(cashboxId, cancellationToken);
            if (access.IsFailure) return access;
        }
        return Result.Success();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<CurrencyCode> GetBaseCurrencyAsync(
        CancellationToken cancellationToken) =>
        await dbContext.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

    private static PartySnapshot? ResolveParty(
        JournalPartyType? partyType,
        int? partyId,
        IReadOnlyDictionary<int, PartySnapshot> partners,
        IReadOnlyDictionary<int, PartySnapshot> employees,
        IReadOnlyDictionary<int, PartySnapshot> drivers,
        IReadOnlyDictionary<int, PartySnapshot> cashboxes)
    {
        if (!partyType.HasValue || !partyId.HasValue)
        {
            return null;
        }

        var source = partyType.Value switch
        {
            JournalPartyType.Customer or JournalPartyType.Supplier => partners,
            JournalPartyType.Employee => employees,
            JournalPartyType.Driver => drivers,
            JournalPartyType.Cashbox => cashboxes,
            _ => null
        };
        return source is not null && source.TryGetValue(partyId.Value, out var party)
            ? party
            : null;
    }

    private sealed record PartySnapshot(string Code, string Name);
}
