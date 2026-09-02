using System.Data;
using static MiniErp.Application.Features.EmployeeOpeningBalances.EmployeeOpeningBalanceErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeOpeningBalances;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.EmployeeOpeningBalances;

public sealed class EmployeeOpeningBalanceService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IExchangeRateResolver exchangeRateResolver,
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null)
    : IEmployeeOpeningBalanceService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<EmployeeOpeningBalanceResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeOpeningBalanceFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new EmployeeOpeningBalanceFilterRequest();
        var query = dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance => balance.CompanyId == companyId)
            .Where(balance =>
                string.IsNullOrWhiteSpace(filters.DocumentNumber) ||
                balance.DocumentNumber.Contains(filters.DocumentNumber.Trim()))
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
                string.IsNullOrWhiteSpace(filters.Search) ||
                balance.DocumentNumber.Contains(filters.Search.Trim()) ||
                balance.Employee.Name.Contains(filters.Search.Trim()) ||
                balance.Employee.Code.Contains(filters.Search.Trim()) ||
                (balance.Notes != null && balance.Notes.Contains(filters.Search.Trim())))
            .OrderByDescending(balance => balance.DocumentDate)
            .ThenByDescending(balance => balance.Id);

        return await paginationService.PaginateAsync<
            EmployeeOpeningBalance,
            EmployeeOpeningBalanceResponse>(
                query,
                pagination,
                cancellationToken);
    }

    public async Task<Result<EmployeeOpeningBalanceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<EmployeeOpeningBalanceResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<EmployeeOpeningBalanceResponse>.Failure(NotFound(id))
            : Result<EmployeeOpeningBalanceResponse>.Success(response);
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

        var response = await ProjectResponseQuery(normalized.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result<EmployeeOpeningBalanceResponse>.Success(response);
    }

    public async Task<Result<EmployeeOpeningBalanceResponse>> UpdateAsync(
        int id,
        EmployeeOpeningBalanceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
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

        return Result<EmployeeOpeningBalanceResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

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
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        return Result.Success();
    }

    private IQueryable<EmployeeOpeningBalanceResponse> ProjectResponseQuery(int id) =>
        dbContext.EmployeeOpeningBalances
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.Id == id)
            .ProjectToType<EmployeeOpeningBalanceResponse>();

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
