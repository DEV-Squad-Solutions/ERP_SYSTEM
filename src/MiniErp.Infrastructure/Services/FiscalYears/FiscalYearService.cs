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
using MiniErp.Infrastructure.Services.ExchangeRates;

namespace MiniErp.Infrastructure.Services.FiscalYears;

public sealed class FiscalYearService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider,
    IAccountingReadinessService? accountingReadinessService = null,
    IDefaultAccountingSetupService? defaultAccountingSetupService = null,
    IFiscalYearInventoryCarryForwardService?
        inventoryCarryForwardService = null)
    : IFiscalYearService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<FiscalYearResponse>>> GetAllAsync(
        PaginationRequest pagination,
        FiscalYearFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new FiscalYearFilterRequest();
        var search = filters.Search?.Trim();

        var query = dbContext.FiscalYears
            .AsNoTracking()
            .Where(fiscalYear => fiscalYear.CompanyId == companyId)
            .Where(fiscalYear =>
                string.IsNullOrEmpty(search) ||
                fiscalYear.Name.Contains(search))
            .Where(fiscalYear =>
                !filters.Status.HasValue ||
                fiscalYear.Status == filters.Status.Value)
            .Where(fiscalYear =>
                !filters.IsCurrent.HasValue ||
                fiscalYear.IsCurrent == filters.IsCurrent.Value)
            .OrderByDescending(fiscalYear => fiscalYear.StartDate)
            .ThenByDescending(fiscalYear => fiscalYear.Id);

        return await paginationService.PaginateAsync<
            FiscalYear,
            FiscalYearResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<FiscalYearSelectResponse>>>
        GetSelectAsync(CancellationToken cancellationToken = default)
    {
        var response = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(fiscalYear => fiscalYear.CompanyId == companyId)
            .OrderByDescending(fiscalYear => fiscalYear.IsCurrent)
            .ThenByDescending(fiscalYear => fiscalYear.StartDate)
            .ThenByDescending(fiscalYear => fiscalYear.Id)
            .ProjectToType<FiscalYearSelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<FiscalYearSelectResponse>>.Success(
            response);
    }

    public async Task<Result<FiscalYearResponse>> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await ProjectResponseQuery(isCurrent: true)
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<FiscalYearResponse>.Failure(CurrentNotFound())
            : Result<FiscalYearResponse>.Success(response);
    }

    public async Task<Result<FiscalYearResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<FiscalYearResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<FiscalYearResponse>.Failure(NotFound(id))
            : Result<FiscalYearResponse>.Success(response);
    }

    public async Task<Result<FiscalYearResponse>> SetCurrentAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<FiscalYearResponse>.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var fiscalYear = await dbContext.FiscalYears
            .FirstOrDefaultAsync(
                year =>
                    year.CompanyId == companyId &&
                    year.Id == id,
                cancellationToken);
        if (fiscalYear is null)
        {
            return Result<FiscalYearResponse>.Failure(NotFound(id));
        }

        if (!fiscalYear.IsCurrent)
        {
            await ClearCurrentAsync(id, cancellationToken);
            fiscalYear.IsCurrent = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);

        return Result<FiscalYearResponse>.Success(response);
    }

    public async Task<Result<FiscalYearResponse>> AddAsync(
        FiscalYearRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartDate >= request.EndDate)
        {
            return Result<FiscalYearResponse>.Failure(DateRangeInvalid());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (await NameExistsAsync(
                request.Name,
                excludedId: null,
                cancellationToken))
        {
            return Result<FiscalYearResponse>.Failure(NameExists(request.Name.Trim()));
        }

        if (await DateRangeOverlapsAsync(
                request.StartDate,
                request.EndDate,
                excludedId: null,
                cancellationToken))
        {
            return Result<FiscalYearResponse>.Failure(DateRangeOverlaps());
        }

        var fiscalYear = request.Adapt<FiscalYear>();
        fiscalYear.CompanyId = companyId;
        fiscalYear.Status = FiscalYearStatus.Open;

        var hasCurrent = await dbContext.FiscalYears
            .AnyAsync(
                fiscalYear =>
                    fiscalYear.CompanyId == companyId &&
                    fiscalYear.IsCurrent,
                cancellationToken);

        if (request.IsCurrent || !hasCurrent)
        {
            await ClearCurrentAsync(null, cancellationToken);
            fiscalYear.IsCurrent = true;
        }

        dbContext.FiscalYears.Add(fiscalYear);
        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureExchangeRateCarryForwardAsync(
            fiscalYear,
            cancellationToken);

        if (defaultAccountingSetupService is not null)
        {
            await defaultAccountingSetupService.EnsureFiscalYearAsync(
                companyId,
                fiscalYear.Id,
                cancellationToken);
        }

        var response = await ProjectResponseQuery(fiscalYear.Id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<FiscalYearResponse>.Success(response);
    }

    public async Task<Result<FiscalYearResponse>> UpdateAsync(
        int id,
        FiscalYearUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<FiscalYearResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<FiscalYearResponse>.Failure(RowVersionRequired());
        }

        if (request.StartDate >= request.EndDate)
        {
            return Result<FiscalYearResponse>.Failure(DateRangeInvalid());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var fiscalYear = await dbContext.FiscalYears
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (fiscalYear is null)
        {
            return Result<FiscalYearResponse>.Failure(NotFound(id));
        }

        if (fiscalYear.Status == FiscalYearStatus.Closed)
        {
            return Result<FiscalYearResponse>.Failure(
                ClosedCannotBeModified());
        }

        if (await NameExistsAsync(
                request.Name,
                excludedId: id,
                cancellationToken))
        {
            return Result<FiscalYearResponse>.Failure(NameExists(request.Name.Trim()));
        }

        if (await DateRangeOverlapsAsync(
                request.StartDate,
                request.EndDate,
                excludedId: id,
                cancellationToken))
        {
            return Result<FiscalYearResponse>.Failure(DateRangeOverlaps());
        }

        if (!fiscalYear.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<FiscalYearResponse>.Failure(Concurrency());
        }

        if (request.IsCurrent)
        {
            await ClearCurrentAsync(id, cancellationToken);
        }

        var entry = dbContext.Entry(fiscalYear);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;
        request.Adapt(fiscalYear);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<FiscalYearResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<FiscalYearResponse>.Success(response);
    }

    public async Task<Result<FiscalYearResponse>> CloseAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await ChangeStatusAsync(
            id,
            FiscalYearStatus.Closed,
            cancellationToken);
    }

    public async Task<Result<FiscalYearResponse>> ReopenAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await ChangeStatusAsync(
            id,
            FiscalYearStatus.Open,
            cancellationToken);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var fiscalYear = await dbContext.FiscalYears
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (fiscalYear is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (fiscalYear.Status == FiscalYearStatus.Closed)
        {
            return Result.Failure(ClosedCannotBeDeleted());
        }

        if (fiscalYear.IsCurrent)
        {
            return Result.Failure(CurrentCannotBeDeleted());
        }

        if (await dbContext.FinancialStatementLines
                .IgnoreQueryFilters()
                .AnyAsync(
                    line =>
                        line.CompanyId == companyId &&
                        line.FiscalYearId == id,
                    cancellationToken) ||
            await dbContext.AccountStatementMappings
                .IgnoreQueryFilters()
                .AnyAsync(
                    mapping =>
                        mapping.CompanyId == companyId &&
                        mapping.FiscalYearId == id,
                    cancellationToken) ||
            await dbContext.AccountMappings
                .IgnoreQueryFilters()
                .AnyAsync(
                    mapping =>
                        mapping.CompanyId == companyId &&
                        mapping.FiscalYearId == id,
                    cancellationToken))
        {
            return Result.Failure(HasAccountingSetup());
        }

        dbContext.FiscalYears.Remove(fiscalYear);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<FiscalYearResponse>> ChangeStatusAsync(
        int id,
        FiscalYearStatus status,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return Result<FiscalYearResponse>.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var fiscalYear = await dbContext.FiscalYears
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (fiscalYear is null)
        {
            return Result<FiscalYearResponse>.Failure(NotFound(id));
        }

        if (fiscalYear.Status == status)
        {
            return Result<FiscalYearResponse>.Failure(
                status == FiscalYearStatus.Closed
                    ? AlreadyClosed()
                    : AlreadyOpen());
        }

        if (status == FiscalYearStatus.Closed)
        {
            var nextStartDate = fiscalYear.StartDate.AddYears(1);
            var nextEndDate = fiscalYear.EndDate.AddYears(1);
            var nextYear = await dbContext.FiscalYears
                .Where(year =>
                    year.CompanyId == companyId &&
                    year.StartDate == nextStartDate &&
                    year.EndDate == nextEndDate)
                .FirstOrDefaultAsync(cancellationToken);
            if (nextYear is null)
            {
                if (await DateRangeOverlapsAsync(
                        nextStartDate,
                        nextEndDate,
                        excludedId: null,
                        cancellationToken))
                {
                    return Result<FiscalYearResponse>.Failure(
                        DateRangeOverlaps());
                }

                nextYear = new FiscalYear
                {
                    CompanyId = companyId,
                    Name = await GenerateNextFiscalYearNameAsync(
                        fiscalYear,
                        cancellationToken),
                    StartDate = nextStartDate,
                    EndDate = nextEndDate,
                    Status = FiscalYearStatus.Open,
                    IsCurrent = false
                };
                dbContext.FiscalYears.Add(nextYear);
                await dbContext.SaveChangesAsync(cancellationToken);

                if (defaultAccountingSetupService is not null)
                {
                    await defaultAccountingSetupService.EnsureFiscalYearAsync(
                        companyId,
                        nextYear.Id,
                        cancellationToken);
                }
            }
            // Check before carrying rates forward: writing a rate into a
            // closed year is rejected by the persistence guard as an
            // exception instead of this documented result.
            if (nextYear.Status != FiscalYearStatus.Open)
            {
                return Result<FiscalYearResponse>.Failure(
                    NextFiscalYearClosed(nextYear.Name));
            }

            await EnsureExchangeRateCarryForwardAsync(
                nextYear,
                cancellationToken);

            var readiness = accountingReadinessService is null
                ? null
                : await accountingReadinessService.GetAsync(
                    fiscalYear.Id,
                    cancellationToken);
            if (readiness is { IsFailure: true })
            {
                return Result<FiscalYearResponse>.Failure(readiness.Errors);
            }

            if (readiness is { Value.IsReady: false })
            {
                var errors = new List<Error>
                {
                    ClosingNotReady(
                        fiscalYear.Name,
                        readiness.Value.Issues.Count)
                };
                errors.AddRange(readiness.Value.Issues.Select(issue =>
                    Error.Conflict(
                        "FiscalYears.ClosingIssue",
                        issue.Message,
                        issue.IssueType)));
                return Result<FiscalYearResponse>.Failure(errors);
            }

            var transfer = await TransferClosingBalancesAsync(
                fiscalYear,
                nextYear,
                cancellationToken);
            if (transfer.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<FiscalYearResponse>.Failure(transfer.Errors);
            }

            if (inventoryCarryForwardService is not null)
            {
                var inventoryTransfer = await inventoryCarryForwardService
                    .CarryForwardAsync(
                        sourceFiscalYearId: fiscalYear.Id,
                        targetFiscalYearId: nextYear.Id,
                        targetStartDate: nextYear.StartDate,
                        sourceFiscalYearName: fiscalYear.Name,
                        cancellationToken: cancellationToken);
                if (inventoryTransfer.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<FiscalYearResponse>.Failure(
                        inventoryTransfer.Errors);
                }
            }

            fiscalYear.Status = status;
            fiscalYear.ClosedOn = timeProvider.GetUtcNow().UtcDateTime;
            if (fiscalYear.IsCurrent)
            {
                fiscalYear.IsCurrent = false;
                nextYear.IsCurrent = true;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var closedResponse = await ProjectResponseQuery(id)
                .FirstAsync(cancellationToken);
            return Result<FiscalYearResponse>.Success(closedResponse);
        }

        var laterClosedYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate > fiscalYear.EndDate &&
                year.Status == FiscalYearStatus.Closed)
            .OrderBy(year => year.StartDate)
            .Select(year => year.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (laterClosedYear is not null)
        {
            return Result<FiscalYearResponse>.Failure(
                LaterFiscalYearClosed(laterClosedYear));
        }

        var existingTransfer = await dbContext.JournalEntries
            .Include(entry => entry.Lines)
            .SingleOrDefaultAsync(entry =>
                entry.CompanyId == companyId &&
                entry.EntryType == JournalEntryType.Opening &&
                entry.SourceType == JournalEntrySourceType.FiscalYearClosing &&
                entry.SourceId == fiscalYear.Id,
                cancellationToken);
        if (existingTransfer is not null)
        {
            dbContext.JournalEntryLines.RemoveRange(existingTransfer.Lines);
            dbContext.JournalEntries.Remove(existingTransfer);
        }

        fiscalYear.Status = status;
        fiscalYear.ClosedOn = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);

        return Result<FiscalYearResponse>.Success(response);
    }

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
