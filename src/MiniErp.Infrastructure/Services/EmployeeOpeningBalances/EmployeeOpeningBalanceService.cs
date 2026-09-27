using System.Data;
using static MiniErp.Application.Features.EmployeeOpeningBalances.EmployeeOpeningBalanceErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeOpeningBalances;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.EmployeeOpeningBalances;

public sealed class EmployeeOpeningBalanceService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
    IExchangeRateResolver exchangeRateResolver,
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null,
    IOpeningBalancePostingService? openingBalancePostingService = null)
    : IEmployeeOpeningBalanceService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<EmployeeOpeningBalanceResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeOpeningBalanceFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new EmployeeOpeningBalanceFilterRequest();
        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.FromDate,
            toDate: filters.ToDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<PagedResponse<EmployeeOpeningBalanceResponse>>.Failure(
                fiscalYear.Errors);
        }

        if (pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return Result<PagedResponse<EmployeeOpeningBalanceResponse>>.Failure(
                PaginationErrors.Invalid());
        }

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

        var persistedRows = dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == fiscalYear.Value.FiscalYearId)
            .Select(balance => new EmployeeOpeningBalanceListRow
            {
                Id = balance.Id,
                CompanyId = balance.CompanyId,
                FiscalYearId = balance.FiscalYearId,
                FiscalYearName = balance.FiscalYear.Name,
                EmployeeId = balance.EmployeeId,
                EmployeeName = balance.Employee.Name,
                EmployeeCode = balance.Employee.Code,
                PayrollEntryId = balance.PayrollEntryId,
                DocumentNumber = balance.DocumentNumber,
                DocumentDate = balance.DocumentDate,
                Currency = balance.Currency,
                BaseCurrency = baseCurrency,
                ExchangeRate = balance.ExchangeRate,
                BalanceType = balance.BalanceType,
                Amount = balance.Amount,
                BaseAmount = balance.BaseAmount,
                Notes = balance.Notes,
                RowVersion = balance.RowVersion,
                IsCarriedForward = false,
                IsReadOnly = balance.PayrollEntryId.HasValue
            });
        var carriedRows = CreateCarriedForwardRows(
            fiscalYearId: fiscalYear.Value.FiscalYearId,
            fiscalYearName: fiscalYear.Value.FiscalYearName,
            baseCurrency: baseCurrency);

        var persisted = await ApplyFilters(persistedRows, filters)
            .ToListAsync(cancellationToken);
        var carriedForward = await ApplyFilters(carriedRows, filters)
            .ToListAsync(cancellationToken);
        var ordered = persisted
            .Concat(carriedForward)
            .OrderByDescending(balance => balance.DocumentDate)
            .ThenByDescending(balance => balance.IsCarriedForward)
            .ThenByDescending(balance => balance.Id);
        var totalCount = persisted.Count + carriedForward.Count;
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        var items = offset >= totalCount
            ? []
            : ordered
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .Select(ToResponse)
                .ToArray();
        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pagination.PageSize);

        return Result<PagedResponse<EmployeeOpeningBalanceResponse>>.Success(
            new PagedResponse<EmployeeOpeningBalanceResponse>(
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: totalPages));
    }

    public async Task<Result<EmployeeOpeningBalanceResponse>> GetByIdAsync(
        int id,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (id == 0)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(InvalidId());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: fiscalYearId,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(
                fiscalYear.Errors);
        }

        if (id < 0)
        {
            var baseCurrency = await dbContext.CompanySettings
                .AsNoTracking()
                .Where(settings => settings.CompanyId == companyId)
                .Select(settings => (CurrencyCode?)settings.BaseCurrency)
                .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;
            var carriedForward = await CreateCarriedForwardRows(
                    fiscalYearId: fiscalYear.Value.FiscalYearId,
                    fiscalYearName: fiscalYear.Value.FiscalYearName,
                    baseCurrency: baseCurrency)
                .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

            return carriedForward is null
                ? Result<EmployeeOpeningBalanceResponse>.Failure(NotFound(id))
                : Result<EmployeeOpeningBalanceResponse>.Success(
                    ToResponse(carriedForward));
        }

        var response = await ProjectResponseQuery(
                id,
                fiscalYear.Value.FiscalYearId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<EmployeeOpeningBalanceResponse>.Failure(NotFound(id))
            : Result<EmployeeOpeningBalanceResponse>.Success(
                ToResponse(response));
    }

    public async Task<Result<EmployeeOpeningBalanceResponse>> AddAsync(
        EmployeeOpeningBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.DocumentDate,
                nameof(EmployeeOpeningBalanceRequest.DocumentDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<EmployeeOpeningBalanceResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        var normalized = request.Adapt<EmployeeOpeningBalance>();

        if (normalized.Currency != CurrencyCode.EGP)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(
                Error.Validation("EmployeeOpeningBalances.CurrencyMustBeEgp", "عملة الرصيد الافتتاحي للموظف يجب أن تكون دائماً بالجنيه المصري (EGP)."));
        }

        var employeeError = await ValidateEmployeeAsync(
            normalized.EmployeeId,
            cancellationToken);
        if (employeeError is not null)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(employeeError);
        }

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            normalized.Currency,
            normalized.DocumentDate,
            request.ExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(
                exchangeRateResult.Error);
        }

        normalized.CompanyId = companyId;
        normalized.DocumentNumber = await EntityIdentifierGenerator
            .GenerateUniqueAsync(
                dbContext,
                prefix: "EOB",
                companyId: companyId,
                existingIdentifiers: dbContext.EmployeeOpeningBalances
                    .IgnoreQueryFilters()
                    .Where(entity => entity.CompanyId == companyId)
                    .Select(entity => entity.DocumentNumber),
                cancellationToken);
        normalized.ApplyExchangeRate(
            exchangeRateResult.Value.ExchangeRateId,
            exchangeRateResult.Value.Rate);

        dbContext.EmployeeOpeningBalances.Add(normalized);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (openingBalancePostingService is not null)
        {
            var postingResult = await openingBalancePostingService
                .SynchronizeEmployeeAsync(normalized.Id, cancellationToken);
            if (postingResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<EmployeeOpeningBalanceResponse>.Failure(
                    postingResult.Errors);
            }
        }

        var response = await ProjectResponseQuery(normalized.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result<EmployeeOpeningBalanceResponse>.Success(
            ToResponse(response));
    }

    public async Task<Result<EmployeeOpeningBalanceResponse>> UpdateAsync(
        int id,
        EmployeeOpeningBalanceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id < 0)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(
                CarriedForwardReadOnly());
        }

        if (id == 0)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: > 0 })
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var normalized = request.Adapt<EmployeeOpeningBalance>();

        var openingBalance = await dbContext.EmployeeOpeningBalances
            .FirstOrDefaultAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.Id == id,
                cancellationToken);
        if (openingBalance is null)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(NotFound(id));
        }

        if (openingBalance.PayrollEntryId.HasValue)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(
                CannotModifyPayrollGeneratedBalance());
        }

        if (!openingBalance.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(Concurrency());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: openingBalance.FiscalYearId,
            fromDate: request.DocumentDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(
                fiscalYear.Errors);
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                openingBalance.DocumentDate,
                nameof(EmployeeOpeningBalanceRequest.DocumentDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<EmployeeOpeningBalanceResponse>.Failure(
                    fiscalYearResult.Errors);
            }

            fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.DocumentDate,
                nameof(EmployeeOpeningBalanceUpdateRequest.DocumentDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<EmployeeOpeningBalanceResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        if (normalized.Currency != CurrencyCode.EGP)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(
                Error.Validation("EmployeeOpeningBalances.CurrencyMustBeEgp", "عملة الرصيد الافتتاحي للموظف يجب أن تكون دائماً بالجنيه المصري (EGP)."));
        }

        var employeeError = await ValidateEmployeeAsync(
            normalized.EmployeeId,
            cancellationToken);
        if (employeeError is not null)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(employeeError);
        }

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            normalized.Currency,
            normalized.DocumentDate,
            request.ExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(
                exchangeRateResult.Error);
        }

        openingBalance.EmployeeId = normalized.EmployeeId;
        openingBalance.DocumentDate = normalized.DocumentDate;
        openingBalance.Currency = normalized.Currency;
        openingBalance.BalanceType = normalized.BalanceType;
        openingBalance.Amount = normalized.Amount;
        openingBalance.Notes = normalized.Notes;
        openingBalance.ApplyExchangeRate(
            exchangeRateResult.Value.ExchangeRateId,
            exchangeRateResult.Value.Rate);

        var entry = dbContext.Entry(openingBalance);
        entry.State = EntityState.Modified;
        entry.Property(balance => balance.RowVersion)
            .OriginalValue = request.RowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            if (openingBalancePostingService is not null)
            {
                var postingResult = await openingBalancePostingService
                    .SynchronizeEmployeeAsync(id, cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result<EmployeeOpeningBalanceResponse>.Failure(
                        postingResult.Errors);
                }
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<EmployeeOpeningBalanceResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result<EmployeeOpeningBalanceResponse>.Success(
            ToResponse(response));
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id < 0)
        {
            return Result.Failure(CarriedForwardReadOnly());
        }

        if (id == 0)
        {
            return Result.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var openingBalance = await dbContext.EmployeeOpeningBalances
            .FirstOrDefaultAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.Id == id,
                cancellationToken);
        if (openingBalance is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                openingBalance.DocumentDate,
                nameof(EmployeeOpeningBalanceRequest.DocumentDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result.Failure(fiscalYearResult.Errors);
            }
        }

        if (openingBalance.PayrollEntryId.HasValue)
        {
            return Result.Failure(CannotDeletePayrollGeneratedBalance());
        }

        dbContext.EmployeeOpeningBalances.Remove(openingBalance);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            if (openingBalancePostingService is not null)
            {
                var postingResult = await openingBalancePostingService
                    .DeleteAsync(
                        JournalEntrySourceType.EmployeeOpeningBalance,
                        openingBalance.Id,
                        cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result.Failure(postingResult.Errors);
                }
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<EmployeeOpeningBalanceListRow> ProjectResponseQuery(
        int id,
        int? fiscalYearId = null) =>
        dbContext.EmployeeOpeningBalances
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.Id == id &&
                (!fiscalYearId.HasValue ||
                 balance.FiscalYearId == fiscalYearId.Value))
            .Select(balance => new EmployeeOpeningBalanceListRow
            {
                Id = balance.Id,
                CompanyId = balance.CompanyId,
                FiscalYearId = balance.FiscalYearId,
                FiscalYearName = balance.FiscalYear.Name,
                EmployeeId = balance.EmployeeId,
                EmployeeName = balance.Employee.Name,
                EmployeeCode = balance.Employee.Code,
                PayrollEntryId = balance.PayrollEntryId,
                DocumentNumber = balance.DocumentNumber,
                DocumentDate = balance.DocumentDate,
                Currency = balance.Currency,
                BaseCurrency = balance.Company.Settings == null
                    ? CurrencyCode.EGP
                    : balance.Company.Settings.BaseCurrency,
                ExchangeRate = balance.ExchangeRate,
                BalanceType = balance.BalanceType,
                Amount = balance.Amount,
                BaseAmount = balance.BaseAmount,
                Notes = balance.Notes,
                RowVersion = balance.RowVersion,
                IsReadOnly = balance.PayrollEntryId.HasValue
            });

    private static IQueryable<EmployeeOpeningBalanceListRow> ApplyFilters(
        IQueryable<EmployeeOpeningBalanceListRow> query,
        EmployeeOpeningBalanceFilterRequest filters)
    {
        var documentNumber = filters.DocumentNumber?.Trim();
        var search = filters.Search?.Trim();
        return query
            .Where(balance =>
                string.IsNullOrEmpty(documentNumber) ||
                balance.DocumentNumber.Contains(documentNumber))
            .Where(balance =>
                !filters.EmployeeId.HasValue ||
                balance.EmployeeId == filters.EmployeeId.Value)
            .Where(balance =>
                !filters.PayrollEntryId.HasValue ||
                balance.PayrollEntryId == filters.PayrollEntryId.Value)
            .Where(balance =>
                !filters.Currency.HasValue ||
                balance.Currency == filters.Currency.Value)
            .Where(balance =>
                !filters.BalanceType.HasValue ||
                balance.BalanceType == filters.BalanceType.Value)
            .Where(balance =>
                !filters.FromDate.HasValue ||
                balance.DocumentDate >= filters.FromDate.Value)
            .Where(balance =>
                !filters.ToDate.HasValue ||
                balance.DocumentDate <= filters.ToDate.Value)
            .Where(balance =>
                string.IsNullOrEmpty(search) ||
                balance.DocumentNumber.Contains(search) ||
                balance.EmployeeName.Contains(search) ||
                balance.EmployeeCode.Contains(search) ||
                (balance.Notes != null && balance.Notes.Contains(search)));
    }

    private IQueryable<EmployeeOpeningBalanceListRow> CreateCarriedForwardRows(
        int fiscalYearId,
        string fiscalYearName,
        CurrencyCode baseCurrency)
    {
        var rows =
            from line in dbContext.JournalEntryLines.AsNoTracking()
            join employee in dbContext.Employees.AsNoTracking()
                on new { line.CompanyId, Id = line.PartyId!.Value }
                equals new { employee.CompanyId, employee.Id }
            join sourceYear in dbContext.FiscalYears.AsNoTracking()
                on new
                {
                    line.CompanyId,
                    Id = line.JournalEntry.SourceId!.Value
                }
                equals new { sourceYear.CompanyId, sourceYear.Id }
            where line.CompanyId == companyId &&
                  line.PartyType == JournalPartyType.Employee &&
                  line.PartyId.HasValue &&
                  line.JournalEntry.FiscalYearId == fiscalYearId &&
                  line.JournalEntry.EntryType == JournalEntryType.Opening &&
                  line.JournalEntry.Status == JournalEntryStatus.Posted &&
                  line.JournalEntry.ReversalOfEntryId == null &&
                  line.JournalEntry.ReversedOn == null &&
                  line.JournalEntry.SourceType ==
                      JournalEntrySourceType.FiscalYearClosing &&
                  line.JournalEntry.SourceId.HasValue
            group line by new
            {
                line.CompanyId,
                JournalEntryId = line.JournalEntryId,
                EmployeeId = line.PartyId!.Value,
                EmployeeName = employee.Name,
                EmployeeCode = employee.Code,
                line.Currency,
                line.JournalEntry.EntryDate,
                line.JournalEntry.EntryNumber,
                line.JournalEntry.SourceNumber,
                SourceFiscalYearId = sourceYear.Id,
                SourceFiscalYearName = sourceYear.Name
            }
            into groupRows
            where groupRows.Sum(line => line.Debit - line.Credit) != 0m
            select new EmployeeOpeningBalanceListRow
            {
                Id = -groupRows.Min(line => line.Id),
                CompanyId = groupRows.Key.CompanyId,
                FiscalYearId = fiscalYearId,
                FiscalYearName = fiscalYearName,
                EmployeeId = groupRows.Key.EmployeeId,
                EmployeeName = groupRows.Key.EmployeeName,
                EmployeeCode = groupRows.Key.EmployeeCode,
                PayrollEntryId = null,
                DocumentNumber = groupRows.Key.SourceNumber ??
                    groupRows.Key.EntryNumber,
                DocumentDate = groupRows.Key.EntryDate,
                Currency = groupRows.Key.Currency,
                BaseCurrency = baseCurrency,
                ExchangeRate = groupRows.Sum(line =>
                        line.TransactionDebit - line.TransactionCredit) == 0m
                    ? 1m
                    : Math.Abs(
                        groupRows.Sum(line => line.Debit - line.Credit) /
                        groupRows.Sum(line =>
                            line.TransactionDebit - line.TransactionCredit)),
                BalanceType = (groupRows.Sum(line =>
                            line.TransactionDebit - line.TransactionCredit) == 0m
                        ? groupRows.Sum(line => line.Debit - line.Credit)
                        : groupRows.Sum(line =>
                            line.TransactionDebit - line.TransactionCredit)) > 0m
                    ? EmployeeBalanceType.Debit
                    : EmployeeBalanceType.Credit,
                Amount = Math.Abs(groupRows.Sum(line =>
                            line.TransactionDebit - line.TransactionCredit) == 0m
                    ? groupRows.Sum(line => line.Debit - line.Credit)
                    : groupRows.Sum(line =>
                        line.TransactionDebit - line.TransactionCredit)),
                BaseAmount = Math.Abs(groupRows.Sum(line =>
                    line.Debit - line.Credit)),
                Notes = "رصيد مرحّل من إقفال السنة المالية " +
                    groupRows.Key.SourceFiscalYearName,
                RowVersion = null,
                IsCarriedForward = true,
                IsReadOnly = true,
                SourceType = JournalEntrySourceType.FiscalYearClosing,
                JournalEntryId = groupRows.Key.JournalEntryId,
                JournalEntryLineId = groupRows.Min(line => line.Id),
                SourceFiscalYearId = groupRows.Key.SourceFiscalYearId,
                SourceFiscalYearName = groupRows.Key.SourceFiscalYearName
            };

        return rows;
    }

    private static EmployeeOpeningBalanceResponse ToResponse(
        EmployeeOpeningBalanceListRow row) =>
        new(
            Id: row.Id,
            CompanyId: row.CompanyId,
            FiscalYearId: row.FiscalYearId,
            FiscalYearName: row.FiscalYearName,
            EmployeeId: row.EmployeeId,
            EmployeeName: row.EmployeeName,
            EmployeeCode: row.EmployeeCode,
            PayrollEntryId: row.PayrollEntryId,
            DocumentNumber: row.DocumentNumber,
            DocumentDate: row.DocumentDate,
            Currency: row.Currency,
            BaseCurrency: row.BaseCurrency,
            ExchangeRate: row.ExchangeRate,
            BalanceType: row.BalanceType,
            Amount: row.Amount,
            BaseAmount: row.BaseAmount,
            Notes: row.Notes,
            RowVersion: row.RowVersion ?? [])
        {
            IsCarriedForward = row.IsCarriedForward,
            IsReadOnly = row.IsReadOnly,
            SourceType = row.SourceType,
            JournalEntryId = row.JournalEntryId,
            JournalEntryLineId = row.JournalEntryLineId,
            SourceFiscalYearId = row.SourceFiscalYearId,
            SourceFiscalYearName = row.SourceFiscalYearName
        };

    private sealed class EmployeeOpeningBalanceListRow
    {
        public int Id { get; init; }
        public int CompanyId { get; init; }
        public int FiscalYearId { get; init; }
        public string FiscalYearName { get; init; } = string.Empty;
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = string.Empty;
        public string EmployeeCode { get; init; } = string.Empty;
        public int? PayrollEntryId { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public DateOnly DocumentDate { get; init; }
        public CurrencyCode Currency { get; init; }
        public CurrencyCode BaseCurrency { get; init; }
        public decimal ExchangeRate { get; init; }
        public EmployeeBalanceType BalanceType { get; init; }
        public decimal Amount { get; init; }
        public decimal BaseAmount { get; init; }
        public string? Notes { get; init; }
        public byte[]? RowVersion { get; init; }
        public bool IsCarriedForward { get; init; }
        public bool IsReadOnly { get; init; }
        public JournalEntrySourceType? SourceType { get; init; }
        public int? JournalEntryId { get; init; }
        public int? JournalEntryLineId { get; init; }
        public int? SourceFiscalYearId { get; init; }
        public string? SourceFiscalYearName { get; init; }
    }

    private async Task<Error?> ValidateEmployeeAsync(
        int employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == employeeId)
            .Select(candidate => new
            {
                candidate.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return EmployeeNotFound(employeeId);
        }

        if (!employee.IsActive)
        {
            return EmployeeInactive();
        }

        return null;
    }
}
