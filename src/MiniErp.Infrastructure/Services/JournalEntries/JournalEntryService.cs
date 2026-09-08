using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.JournalEntries.JournalEntryErrors;

namespace MiniErp.Infrastructure.Services.JournalEntries;

public sealed class JournalEntryService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider)
    : IJournalEntryService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<JournalEntryResponse>>> GetAllAsync(
        PaginationRequest pagination,
        JournalEntryFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        if (pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return Result<PagedResponse<JournalEntryResponse>>.Failure(
                PaginationErrors.Invalid());
        }

        filters ??= new JournalEntryFilterRequest();
        var search = filters.Search?.Trim();
        var query = dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.CompanyId == companyId)
            .Where(entry =>
                string.IsNullOrEmpty(search) ||
                entry.EntryNumber.Contains(search) ||
                entry.Description.Contains(search))
            .Where(entry =>
                !filters.FiscalYearId.HasValue ||
                entry.FiscalYearId == filters.FiscalYearId.Value)
            .Where(entry =>
                !filters.EntryType.HasValue ||
                entry.EntryType == filters.EntryType.Value)
            .Where(entry =>
                !filters.Status.HasValue ||
                entry.Status == filters.Status.Value)
            .Where(entry =>
                !filters.FromDate.HasValue ||
                entry.EntryDate >= filters.FromDate.Value)
            .Where(entry =>
                !filters.ToDate.HasValue ||
                entry.EntryDate <= filters.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        IReadOnlyList<JournalEntryResponse> items = [];
        if (offset < totalCount)
        {
            var ids = await query
                .OrderByDescending(entry => entry.EntryDate)
                .ThenByDescending(entry => entry.Id)
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .Select(entry => entry.Id)
                .ToArrayAsync(cancellationToken);

            items = (await LoadResponsesAsync(ids, cancellationToken))
                .OrderByDescending(entry => entry.EntryDate)
                .ThenByDescending(entry => entry.Id)
                .ToArray();
        }

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pagination.PageSize);
        return Result<PagedResponse<JournalEntryResponse>>.Success(
            new PagedResponse<JournalEntryResponse>(
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: totalPages));
    }

    public async Task<Result<JournalEntryResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<JournalEntryResponse>.Failure(InvalidId());
        }

        var response = (await LoadResponsesAsync([id], cancellationToken))
            .SingleOrDefault();
        return response is null
            ? Result<JournalEntryResponse>.Failure(NotFound(id))
            : Result<JournalEntryResponse>.Success(response);
    }

    public async Task<Result<JournalEntryResponse>> AddAsync(
        JournalEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EntryType == JournalEntryType.Automatic)
        {
            return Result<JournalEntryResponse>.Failure(
                AutomaticCannotBeCreatedManually());
        }

        var balanceValidation = ValidateBalance(request.Lines);
        if (balanceValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                balanceValidation.Errors);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var fiscalYearValidation = await ValidateFiscalYearAsync(
            request.FiscalYearId,
            request.EntryDate,
            cancellationToken);
        if (fiscalYearValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                fiscalYearValidation.Errors);
        }

        var accountValidation = await ValidateAccountsAsync(
            request.Lines,
            request.FiscalYearId,
            cancellationToken);
        if (accountValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                accountValidation.Errors);
        }

        var entryNumber = await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "JV",
            companyId,
            dbContext.JournalEntries
                .IgnoreQueryFilters()
                .Where(entry => entry.CompanyId == companyId)
                .Select(entry => entry.EntryNumber),
            cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entry = new JournalEntry
        {
            CompanyId = companyId,
            FiscalYearId = request.FiscalYearId,
            EntryNumber = entryNumber,
            EntryDate = request.EntryDate,
            Description = request.Description.Trim(),
            EntryType = request.EntryType,
            Status = JournalEntryStatus.Posted,
            PostedOn = now,
            Lines = request.Lines.Select(line => new JournalEntryLine
            {
                CompanyId = companyId,
                AccountId = line.AccountId,
                PartyType = line.PartyType,
                PartyId = line.PartyId,
                Description = NormalizeOptional(line.Description),
                Debit = line.Debit,
                Credit = line.Credit
            }).ToList()
        };

        dbContext.JournalEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = (await LoadResponsesAsync(
            [entry.Id],
            cancellationToken)).Single();
        return Result<JournalEntryResponse>.Success(response);
    }

    public async Task<Result<JournalEntryResponse>> UpdateAsync(
        int id,
        JournalEntryUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<JournalEntryResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<JournalEntryResponse>.Failure(RowVersionRequired());
        }

        var balanceValidation = ValidateBalance(request.Lines);
        if (balanceValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                balanceValidation.Errors);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var entry = await dbContext.JournalEntries
            .Include(journalEntry => journalEntry.FiscalYear)
            .Include(journalEntry => journalEntry.Lines)
            .FirstOrDefaultAsync(
                journalEntry =>
                    journalEntry.CompanyId == companyId &&
                    journalEntry.Id == id,
                cancellationToken);
        if (entry is null)
        {
            return Result<JournalEntryResponse>.Failure(NotFound(id));
        }

        if (!entry.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<JournalEntryResponse>.Failure(Concurrency());
        }

        if (entry.EntryType == JournalEntryType.Automatic)
        {
            return Result<JournalEntryResponse>.Failure(AutomaticReadOnly());
        }

        if (entry.Status == JournalEntryStatus.Reversed ||
            entry.ReversalOfEntryId.HasValue)
        {
            return Result<JournalEntryResponse>.Failure(ReversedReadOnly());
        }

        var fiscalYearValidation = await ValidateFiscalYearAsync(
            request.FiscalYearId,
            request.EntryDate,
            cancellationToken);
        if (fiscalYearValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                fiscalYearValidation.Errors);
        }

        if (entry.FiscalYearId != request.FiscalYearId ||
            entry.EntryDate != request.EntryDate ||
            entry.FiscalYear.Status != FiscalYearStatus.Open)
        {
            var oldFiscalYearValidation = await ValidateFiscalYearAsync(
                entry.FiscalYearId,
                entry.EntryDate,
                cancellationToken);
            if (oldFiscalYearValidation.IsFailure)
            {
                return Result<JournalEntryResponse>.Failure(
                    oldFiscalYearValidation.Errors);
            }
        }

        var accountValidation = await ValidateAccountsAsync(
            request.Lines,
            request.FiscalYearId,
            cancellationToken);
        if (accountValidation.IsFailure)
        {
            return Result<JournalEntryResponse>.Failure(
                accountValidation.Errors);
        }

        var entryState = dbContext.Entry(entry);
        entryState.Property(journalEntry => journalEntry.RowVersion)
            .OriginalValue = request.RowVersion;
        entry.FiscalYearId = request.FiscalYearId;
        entry.EntryDate = request.EntryDate;
        entry.Description = request.Description.Trim();

        dbContext.JournalEntryLines.RemoveRange(entry.Lines);
        entry.Lines = request.Lines.Select(line => new JournalEntryLine
        {
            CompanyId = companyId,
            AccountId = line.AccountId,
            PartyType = line.PartyType,
            PartyId = line.PartyId,
            Description = NormalizeOptional(line.Description),
            Debit = line.Debit,
            Credit = line.Credit
        }).ToList();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var response = (await LoadResponsesAsync([id], cancellationToken))
                .Single();
            await transaction.CommitAsync(cancellationToken);
            return Result<JournalEntryResponse>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<JournalEntryResponse>.Failure(Concurrency());
        }
    }

    public async Task<Result> DeleteAsync(
        int id,
        byte[]? rowVersion,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        if (rowVersion is not { Length: 8 })
        {
            return Result.Failure(RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var entry = await dbContext.JournalEntries
            .Include(journalEntry => journalEntry.FiscalYear)
            .Include(journalEntry => journalEntry.Lines)
            .FirstOrDefaultAsync(
                journalEntry =>
                    journalEntry.CompanyId == companyId &&
                    journalEntry.Id == id,
                cancellationToken);
        if (entry is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (!entry.RowVersion.SequenceEqual(rowVersion))
        {
            return Result.Failure(Concurrency());
        }

        if (entry.EntryType == JournalEntryType.Automatic)
        {
            return Result.Failure(AutomaticReadOnly());
        }

        if (entry.Status == JournalEntryStatus.Reversed ||
            entry.ReversalOfEntryId.HasValue)
        {
            return Result.Failure(ReversedReadOnly());
        }

        var fiscalYearValidation = await ValidateFiscalYearAsync(
            entry.FiscalYearId,
            entry.EntryDate,
            cancellationToken);
        if (fiscalYearValidation.IsFailure)
        {
            return Result.Failure(fiscalYearValidation.Errors);
        }

        dbContext.JournalEntryLines.RemoveRange(entry.Lines);
        dbContext.JournalEntries.Remove(entry);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }
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
            .Select(party => new { party.Id, party.IsActive })
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
        var cashboxes = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashboxIds.Contains(cashbox.Id))
            .Select(cashbox => new { cashbox.Id, cashbox.IsActive })
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
                        ? (Found: true, party.IsActive)
                        : (Found: false, IsActive: false),
                JournalPartyType.Employee =>
                    employees.TryGetValue(line.PartyId.Value, out var party)
                        ? (Found: true, party.IsActive)
                        : (Found: false, IsActive: false),
                JournalPartyType.Driver =>
                    drivers.TryGetValue(line.PartyId.Value, out var party)
                        ? (Found: true, party.IsActive)
                        : (Found: false, IsActive: false),
                JournalPartyType.Cashbox =>
                    cashboxes.TryGetValue(line.PartyId.Value, out var party)
                        ? (Found: true, party.IsActive)
                        : (Found: false, IsActive: false),
                _ => (Found: false, IsActive: false)
            };
            if (!partyState.Found)
            {
                errors.Add(PartyNotFound(line.PartyId.Value, index));
            }
            else if (!partyState.IsActive)
            {
                errors.Add(PartyInactive(line.PartyId.Value, index));
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
        CancellationToken cancellationToken)
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
                ids.Contains(entry.Id))
            .ToListAsync(cancellationToken);

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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
