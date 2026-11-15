using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.DriverOpeningBalances;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.DriverOpeningBalances.DriverOpeningBalanceErrors;

namespace MiniErp.Infrastructure.Services.DriverOpeningBalances;

public sealed class DriverOpeningBalanceService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
    IOpeningBalancePostingService openingBalancePostingService)
    : IDriverOpeningBalanceService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<DriverOpeningBalanceResponse>>> GetAllAsync(
        PaginationRequest pagination,
        int? fiscalYearId,
        int? driverId,
        CancellationToken cancellationToken = default)
    {
        if (pagination.PageNumber <= 0 || pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return Result<PagedResponse<DriverOpeningBalanceResponse>>.Failure(PaginationErrors.Invalid());
        }
        if (driverId <= 0)
        {
            return Result<PagedResponse<DriverOpeningBalanceResponse>>.Failure(DriverNotFound(driverId.Value));
        }

        var scope = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId, cancellationToken: cancellationToken);
        if (scope.IsFailure)
        {
            return Result<PagedResponse<DriverOpeningBalanceResponse>>.Failure(scope.Errors);
        }

        var manual = await ManualRows(scope.Value.FiscalYearId)
            .Where(row => !driverId.HasValue || row.DriverId == driverId.Value)
            .ToListAsync(cancellationToken);
        var carried = await LoadCarriedAsync(scope.Value, driverId, cancellationToken);
        var totalCount = manual.Count + carried.Count;
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        var items = offset >= totalCount
            ? []
            : manual.Select(row => ToResponse(row, scope.Value))
                .Concat(carried)
                .OrderByDescending(row => row.DocumentDate)
                .ThenByDescending(row => row.IsCarriedForward)
                .ThenBy(row => row.DriverId)
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .ToArray();

        return Result<PagedResponse<DriverOpeningBalanceResponse>>.Success(
            new PagedResponse<DriverOpeningBalanceResponse>(
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: (int)Math.Ceiling(totalCount / (double)pagination.PageSize)));
    }

    public async Task<Result<DriverOpeningBalanceResponse>> GetByIdAsync(
        int id, int? fiscalYearId, CancellationToken cancellationToken = default)
    {
        if (id is 0 or int.MinValue)
        {
            return Result<DriverOpeningBalanceResponse>.Failure(InvalidId());
        }
        var scope = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId, cancellationToken: cancellationToken);
        if (scope.IsFailure)
        {
            return Result<DriverOpeningBalanceResponse>.Failure(scope.Errors);
        }

        if (id < 0)
        {
            var carried = await LoadCarriedAsync(scope.Value, -id, cancellationToken);
            return carried.Count == 0
                ? Result<DriverOpeningBalanceResponse>.Failure(NotFound(id))
                : Result<DriverOpeningBalanceResponse>.Success(carried[0]);
        }

        var balance = await ManualRows(scope.Value.FiscalYearId)
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return balance is null
            ? Result<DriverOpeningBalanceResponse>.Failure(NotFound(id))
            : Result<DriverOpeningBalanceResponse>.Success(ToResponse(balance, scope.Value));
    }

    public async Task<Result<DriverOpeningBalanceResponse>> AddAsync(
        DriverOpeningBalanceRequest request, CancellationToken cancellationToken = default)
    {
        if (Validate(request) is { } error)
        {
            return Result<DriverOpeningBalanceResponse>.Failure(error);
        }

        return await WriteAsync(async () =>
        {
            var scope = await ResolveOpenYearAsync(request.FiscalYearId, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(scope.Errors);
            }
            var driver = await dbContext.Drivers.AsNoTracking().SingleOrDefaultAsync(
                row => row.CompanyId == companyId && row.Id == request.DriverId, cancellationToken);
            if (driver is null)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(DriverNotFound(request.DriverId));
            }
            if (!driver.IsActive)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(InactiveDriver());
            }
            if (await dbContext.DriverOpeningBalances.AnyAsync(row =>
                row.CompanyId == companyId && row.FiscalYearId == request.FiscalYearId &&
                row.DriverId == request.DriverId, cancellationToken))
            {
                return Result<DriverOpeningBalanceResponse>.Failure(Duplicate());
            }
            if (await FindExistingLedgerOpeningAsync(scope.Value, request.DriverId, cancellationToken) is { } conflict)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(conflict);
            }

            var balance = new DriverOpeningBalance
            {
                CompanyId = companyId,
                FiscalYearId = scope.Value.FiscalYearId,
                DriverId = driver.Id,
                DocumentDate = scope.Value.StartDate,
                DocumentNumber = await EntityIdentifierGenerator.GenerateUniqueAsync(
                    dbContext,
                    prefix: "DOB",
                    companyId: companyId,
                    existingIdentifiers: dbContext.DriverOpeningBalances.IgnoreQueryFilters()
                        .Where(row => row.CompanyId == companyId).Select(row => row.DocumentNumber),
                    cancellationToken: cancellationToken),
                Currency = await GetBaseCurrencyAsync(cancellationToken)
            };
            ApplyDetails(balance, request);
            dbContext.DriverOpeningBalances.Add(balance);
            return await SaveAndPostAsync(balance, driver.Name, scope.Value, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Result<DriverOpeningBalanceResponse>> UpdateAsync(
        int id, DriverOpeningBalanceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if ((ValidateWrite(id, request.RowVersion) ?? Validate(request)) is { } error)
        {
            return Result<DriverOpeningBalanceResponse>.Failure(error);
        }

        return await WriteAsync(async () =>
        {
            var balance = await FindManualAsync(id, cancellationToken);
            if (balance is null)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(NotFound(id));
            }
            if (request.DriverId != balance.DriverId || request.FiscalYearId != balance.FiscalYearId)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(ScopeImmutable());
            }
            var scope = await ResolveOpenYearAsync(balance.FiscalYearId, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(scope.Errors);
            }
            if (!balance.RowVersion.AsSpan().SequenceEqual(request.RowVersion))
            {
                return Result<DriverOpeningBalanceResponse>.Failure(Concurrency());
            }
            if (await FindExistingLedgerOpeningAsync(scope.Value, balance.DriverId, cancellationToken) is { } conflict)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(conflict);
            }

            ApplyDetails(balance, request);
            return await SaveAndPostAsync(balance, balance.Driver.Name, scope.Value, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Result> DeleteAsync(int id, byte[]? rowVersion, CancellationToken cancellationToken = default)
    {
        if (ValidateWrite(id, rowVersion) is { } error)
        {
            return Result.Failure(error);
        }

        return await WriteAsync(async () =>
        {
            var balance = await FindManualAsync(id, cancellationToken);
            if (balance is null)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(NotFound(id));
            }
            var scope = await ResolveOpenYearAsync(balance.FiscalYearId, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(scope.Errors);
            }
            if (!balance.RowVersion.AsSpan().SequenceEqual(rowVersion))
            {
                return Result<DriverOpeningBalanceResponse>.Failure(Concurrency());
            }

            var posting = await openingBalancePostingService.DeleteAsync(
                JournalEntrySourceType.DriverOpeningBalance, id, cancellationToken);
            if (posting.IsFailure)
            {
                return Result<DriverOpeningBalanceResponse>.Failure(posting.Errors);
            }
            dbContext.DriverOpeningBalances.Remove(balance);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<DriverOpeningBalanceResponse>.Success(ToResponse(balance, scope.Value));
        }, cancellationToken);
    }

    private async Task<Result<DriverOpeningBalanceResponse>> WriteAsync(
        Func<Task<Result<DriverOpeningBalanceResponse>>> action,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var committed = false;
        try
        {
            var result = await action();
            if (result.IsSuccess)
            {
                await transaction.CommitAsync(cancellationToken);
                committed = true;
            }
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<DriverOpeningBalanceResponse>.Failure(Concurrency());
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            return Result<DriverOpeningBalanceResponse>.Failure(Concurrency());
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            return Result<DriverOpeningBalanceResponse>.Failure(Concurrency());
        }
        finally
        {
            if (!committed)
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Result<DriverOpeningBalanceResponse>> SaveAndPostAsync(
        DriverOpeningBalance balance, string driverName, FiscalYearQueryScope scope, CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        var posting = await openingBalancePostingService.SynchronizeDriverAsync(balance.Id, cancellationToken);
        if (posting.IsFailure)
        {
            return Result<DriverOpeningBalanceResponse>.Failure(posting.Errors);
        }

        // Read the store-generated row version from the same transaction.
        await dbContext.Entry(balance).ReloadAsync(cancellationToken);
        return Result<DriverOpeningBalanceResponse>.Success(ToResponse(balance, scope, driverName));
    }

    private async Task<Result<FiscalYearQueryScope>> ResolveOpenYearAsync(
        int fiscalYearId, CancellationToken cancellationToken)
    {
        var scope = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId, cancellationToken: cancellationToken);
        if (scope.IsFailure)
        {
            return scope;
        }
        return scope.Value.Status == FiscalYearStatus.Open
            ? scope
            : Result<FiscalYearQueryScope>.Failure(FiscalYearClosed(scope.Value.FiscalYearName));
    }

    private Task<DriverOpeningBalance?> FindManualAsync(int id, CancellationToken cancellationToken) =>
        dbContext.DriverOpeningBalances.Include(row => row.Driver)
            .SingleOrDefaultAsync(row => row.CompanyId == companyId && row.Id == id, cancellationToken);

    private IQueryable<DriverOpeningBalance> ManualRows(int fiscalYearId) =>
        dbContext.DriverOpeningBalances.AsNoTracking().Include(row => row.Driver)
            .Where(row => row.CompanyId == companyId && row.FiscalYearId == fiscalYearId);

    private IQueryable<Domain.Entities.Accounting.JournalEntryLine> CarriedLines(int fiscalYearId) =>
        DriverOpeningBalanceLedgerQueries.CarriedLines(dbContext, companyId, fiscalYearId);

    private async Task<Error?> FindExistingLedgerOpeningAsync(
        FiscalYearQueryScope scope, int driverId, CancellationToken cancellationToken)
    {
        var carried = await CarriedLines(scope.FiscalYearId)
            .Where(line => line.PartyId == driverId)
            .SumAsync(line => (decimal?)(line.Debit - line.Credit), cancellationToken);
        if (carried.GetValueOrDefault() != 0m)
        {
            return CarriedForwardExists();
        }
        var legacy = await DriverOpeningBalanceLedgerQueries.LegacyOpeningLines(
                dbContext, companyId, scope.FiscalYearId, scope.StartDate)
            .Where(line => line.PartyId == driverId)
            .SumAsync(line => (decimal?)(line.Debit - line.Credit), cancellationToken);
        return legacy.GetValueOrDefault() == 0m ? null : LegacyOpeningExists();
    }

    private async Task<IReadOnlyList<DriverOpeningBalanceResponse>> LoadCarriedAsync(
        FiscalYearQueryScope scope, int? driverId, CancellationToken cancellationToken)
    {
        var netByDriver = CarriedLines(scope.FiscalYearId)
            .Where(line => !driverId.HasValue || line.PartyId == driverId.Value)
            .GroupBy(line => line.PartyId!.Value)
            .Select(group => new
            {
                DriverId = group.Key,
                Net = group.Sum(line => line.Debit - line.Credit),
                DocumentNumber = group.Max(line => line.JournalEntry.SourceNumber ?? line.JournalEntry.EntryNumber)
            })
            .Where(row => row.Net != 0m);
        var rows = await (
            from row in netByDriver
            join driver in dbContext.Drivers.AsNoTracking().Where(driver => driver.CompanyId == companyId)
                on row.DriverId equals driver.Id
            select new { row.DriverId, row.Net, row.DocumentNumber, driver.Name })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }
        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);
        return rows.Select(row => new DriverOpeningBalanceResponse(
            Id: -row.DriverId,
            DriverId: row.DriverId,
            DriverName: row.Name,
            FiscalYearId: scope.FiscalYearId,
            FiscalYearName: scope.FiscalYearName,
            DocumentNumber: row.DocumentNumber!,
            DocumentDate: scope.StartDate,
            Amount: Math.Abs(row.Net),
            Currency: baseCurrency,
            BalanceType: row.Net > 0m ? DriverOpeningBalanceType.Receivable : DriverOpeningBalanceType.Payable,
            Notes: null,
            IsCarriedForward: true,
            IsReadOnly: true,
            RowVersion: null)).ToArray();
    }

    private async Task<CurrencyCode> GetBaseCurrencyAsync(CancellationToken cancellationToken) =>
        await dbContext.CompanySettings.AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

    private static void ApplyDetails(DriverOpeningBalance balance, DriverOpeningBalanceRequest request)
    {
        balance.Amount = request.Amount;
        balance.BalanceType = request.BalanceType;
        balance.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private static Error? Validate(DriverOpeningBalanceRequest request)
    {
        if (request.DriverId <= 0)
        {
            return DriverNotFound(request.DriverId);
        }
        if (request.FiscalYearId <= 0)
        {
            return FiscalYearErrors.InvalidId();
        }
        if (request.Amount is <= 0m or > DriverOpeningBalanceRequest.MaximumAmount ||
            decimal.Round(request.Amount, 2) != request.Amount)
        {
            return InvalidAmount();
        }
        if (!Enum.IsDefined(request.BalanceType))
        {
            return InvalidBalanceType();
        }
        return request.Notes?.Length > DriverOpeningBalanceRequest.MaximumNotesLength ? NotesTooLong() : null;
    }

    private static Error? ValidateWrite(int id, byte[]? rowVersion) =>
        id == 0 ? InvalidId() : id < 0 ? ReadOnly() : rowVersion?.Length != 8 ? RowVersionRequired() : null;

    private static DriverOpeningBalanceResponse ToResponse(
        DriverOpeningBalance row, FiscalYearQueryScope scope, string? driverName = null) =>
        new(
            Id: row.Id,
            DriverId: row.DriverId,
            DriverName: driverName ?? row.Driver.Name,
            FiscalYearId: row.FiscalYearId,
            FiscalYearName: scope.FiscalYearName,
            DocumentNumber: row.DocumentNumber,
            DocumentDate: row.DocumentDate,
            Amount: row.Amount,
            Currency: row.Currency,
            BalanceType: row.BalanceType,
            Notes: row.Notes,
            IsCarriedForward: false,
            IsReadOnly: scope.Status != FiscalYearStatus.Open,
            RowVersion: row.RowVersion);
}
