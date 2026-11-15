using System.Data;
using Mapster;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Enums;
using static MiniErp.Application.Features.DriverTrips.DriverTripErrors;

namespace MiniErp.Infrastructure.Services.DriverTrips;

public sealed partial class DriverTripService
{
    public async Task<Result<DriverTripCostResponse>> GetByIdAsync(
        int id, int? fiscalYearId = null, CancellationToken cancellationToken = default)
    {
        var scope = await fiscalYearQueryScopeResolver.ResolveAsync(fiscalYearId, cancellationToken: cancellationToken);
        if (scope.IsFailure)
        {
            return Result<DriverTripCostResponse>.Failure(scope.Errors);
        }

        var row = await ReadRows(scope.Value.FiscalYearId)
            .SingleOrDefaultAsync(row => row.DriverTripId == id, cancellationToken);
        return row is null
            ? Result<DriverTripCostResponse>.Failure(TripsNotFound())
            : Result<DriverTripCostResponse>.Success(row);
    }

    public async Task<Result<DriverTripCostResponse>> AddAsync(
        DriverTripRequest request, CancellationToken cancellationToken = default)
    {
        if (Validate(request) is { } error)
        {
            return Result<DriverTripCostResponse>.Failure(error);
        }

        return await WriteStandaloneAsync(async () =>
        {
            var scope = await ResolveOpenYearAsync(request.FiscalYearId, request.TripDate, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<DriverTripCostResponse>.Failure(scope.Errors);
            }

            if (await ValidatePartiesAsync(request, null, cancellationToken) is { } partyError)
            {
                return Result<DriverTripCostResponse>.Failure(partyError);
            }

            var trip = new DriverTrip { CompanyId = companyId, FiscalYearId = request.FiscalYearId };
            Apply(trip, request);
            dbContext.DriverTrips.Add(trip);
            return await SaveStandaloneAsync(trip, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Result<DriverTripCostResponse>> UpdateAsync(
        int id, DriverTripUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if ((ValidateVersion(id, request.RowVersion) ?? Validate(request)) is { } error)
        {
            return Result<DriverTripCostResponse>.Failure(error);
        }

        return await WriteStandaloneAsync(async () =>
        {
            var trip = await FindTripAsync(id, cancellationToken);
            if (trip is null)
            {
                return Result<DriverTripCostResponse>.Failure(TripsNotFound());
            }

            if (trip.InvoiceId.HasValue)
            {
                return Result<DriverTripCostResponse>.Failure(InvoiceOwned());
            }

            if (trip.FiscalYearId != request.FiscalYearId)
            {
                return Result<DriverTripCostResponse>.Failure(FiscalYearImmutable());
            }

            var scope = await ResolveOpenYearAsync(trip.FiscalYearId, request.TripDate, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<DriverTripCostResponse>.Failure(scope.Errors);
            }

            if (!trip.RowVersion.AsSpan().SequenceEqual(request.RowVersion))
            {
                return Result<DriverTripCostResponse>.Failure(Concurrency(id));
            }

            var identityChanged = trip.DriverId != request.DriverId ||
                trip.BusinessPartnerId != request.BusinessPartnerId || trip.TripDate != request.TripDate;
            if (identityChanged && await HasVoucherHistoryAsync(id, cancellationToken))
            {
                return Result<DriverTripCostResponse>.Failure(LinkedVoucher());
            }

            if (await ValidatePartiesAsync(request, trip, cancellationToken) is { } partyError)
            {
                return Result<DriverTripCostResponse>.Failure(partyError);
            }

            trip = TrackForWrite(trip);
            Apply(trip, request);
            return await SaveStandaloneAsync(trip, cancellationToken);
        }, cancellationToken, id);
    }

    public async Task<Result> DeleteAsync(int id, byte[]? rowVersion, CancellationToken cancellationToken = default)
    {
        if (ValidateVersion(id, rowVersion) is { } error)
        {
            return Result.Failure(error);
        }

        return await WriteStandaloneAsync(async () =>
        {
            var trip = await FindTripAsync(id, cancellationToken);
            if (trip is null)
            {
                return Result<DriverTripCostResponse>.Failure(TripsNotFound());
            }

            if (trip.InvoiceId.HasValue)
            {
                return Result<DriverTripCostResponse>.Failure(InvoiceOwned());
            }

            var scope = await ResolveOpenYearAsync(trip.FiscalYearId, trip.TripDate, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<DriverTripCostResponse>.Failure(scope.Errors);
            }

            if (!trip.RowVersion.AsSpan().SequenceEqual(rowVersion))
            {
                return Result<DriverTripCostResponse>.Failure(Concurrency(id));
            }

            if (await HasVoucherHistoryAsync(id, cancellationToken))
            {
                return Result<DriverTripCostResponse>.Failure(LinkedVoucher());
            }

            var response = await ReadRows(trip.FiscalYearId)
                .SingleAsync(row => row.DriverTripId == id, cancellationToken);
            var posting = await driverTripPostingService!.DeleteAsync(id, cancellationToken);
            if (posting.IsFailure)
            {
                return Result<DriverTripCostResponse>.Failure(posting.Errors);
            }

            dbContext.DriverTrips.Remove(TrackForWrite(trip));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<DriverTripCostResponse>.Success(response);
        }, cancellationToken, id);
    }

    private async Task<Result<DriverTripCostResponse>> WriteStandaloneAsync(
        Func<Task<Result<DriverTripCostResponse>>> action, CancellationToken cancellationToken, int? id = null)
    {
        if (driverTripPostingService is null)
        {
            return Result<DriverTripCostResponse>.Failure(PostingUnavailable());
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
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
            return Result<DriverTripCostResponse>.Failure(Concurrency(id));
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            return Result<DriverTripCostResponse>.Failure(Concurrency(id));
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            return Result<DriverTripCostResponse>.Failure(Concurrency(id));
        }
        finally
        {
            if (!committed)
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Result<DriverTripCostResponse>> SaveStandaloneAsync(DriverTrip trip, CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        var posting = await driverTripPostingService!.SynchronizeAsync(trip.Id, cancellationToken);
        if (posting.IsFailure)
        {
            return Result<DriverTripCostResponse>.Failure(posting.Errors);
        }

        // Reload the generated version, including providers whose version is advanced by a trigger.
        await dbContext.Entry(trip).ReloadAsync(cancellationToken);
        return Result<DriverTripCostResponse>.Success(await ReadRows(trip.FiscalYearId)
            .SingleAsync(row => row.DriverTripId == trip.Id, cancellationToken));
    }

    private IQueryable<DriverTripCostResponse> ReadRows(int fiscalYearId) => dbContext.DriverTrips.AsNoTracking()
        .Where(trip => trip.CompanyId == companyId && trip.FiscalYearId == fiscalYearId)
        .ProjectToType<DriverTripCostResponse>();

    private Task<DriverTrip?> FindTripAsync(int id, CancellationToken cancellationToken) => dbContext.DriverTrips.AsNoTracking()
        .SingleOrDefaultAsync(trip => trip.CompanyId == companyId && trip.Id == id, cancellationToken);

    private DriverTrip TrackForWrite(DriverTrip snapshot)
    {
        var existing = dbContext.DriverTrips.Local.FirstOrDefault(trip => trip.Id == snapshot.Id);
        if (existing is null)
        {
            return dbContext.DriverTrips.Attach(snapshot).Entity;
        }

        var entry = dbContext.Entry(existing);
        entry.CurrentValues.SetValues(snapshot);
        entry.OriginalValues.SetValues(snapshot);
        entry.State = EntityState.Unchanged;
        return existing;
    }

    private Task<bool> HasVoucherHistoryAsync(int id, CancellationToken cancellationToken) => dbContext.CashVouchers.IgnoreQueryFilters()
        .AnyAsync(voucher => voucher.CompanyId == companyId && voucher.DriverTripId == id, cancellationToken);

    private async Task<Result<FiscalYearQueryScope>> ResolveOpenYearAsync(int fiscalYearId, DateOnly date, CancellationToken cancellationToken)
    {
        var scope = await fiscalYearQueryScopeResolver.ResolveAsync(fiscalYearId, cancellationToken: cancellationToken);
        if (scope.IsFailure) return scope;
        if (date < scope.Value.StartDate || date > scope.Value.EndDate)
            return Result<FiscalYearQueryScope>.Failure(FiscalYearErrors.QueryDateOutsideRange(date, nameof(DriverTripRequest.TripDate),
                scope.Value.FiscalYearName, scope.Value.StartDate, scope.Value.EndDate));
        return scope.Value.Status == FiscalYearStatus.Open ? scope : Result<FiscalYearQueryScope>.Failure(
            FiscalYearErrors.Closed(date, scope.Value.FiscalYearName, nameof(DriverTripRequest.TripDate)));
    }

    private async Task<Error?> ValidatePartiesAsync(DriverTripRequest request, DriverTrip? existing, CancellationToken cancellationToken)
    {
        if (!await dbContext.Drivers.AnyAsync(driver => driver.CompanyId == companyId && driver.Id == request.DriverId &&
                (driver.IsActive || (existing != null && existing.DriverId == driver.Id)), cancellationToken)) return DriverUnavailable();
        if (request.BusinessPartnerId.HasValue && !await dbContext.BusinessPartners.AnyAsync(partner => partner.CompanyId == companyId &&
                partner.Id == request.BusinessPartnerId.Value && (partner.IsActive || (existing != null && existing.BusinessPartnerId == partner.Id)), cancellationToken))
            return PartnerUnavailable();
        return null;
    }

    private static void Apply(DriverTrip trip, DriverTripRequest request)
    {
        trip.DriverId = request.DriverId;
        trip.BusinessPartnerId = request.BusinessPartnerId;
        trip.TripDate = request.TripDate;
        trip.ActualDriverName = string.IsNullOrWhiteSpace(request.ActualDriverName) ? null : request.ActualDriverName.Trim();
        trip.Cost = request.Cost;
        trip.CostNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private static Error? Validate(DriverTripRequest request) => request.DriverId <= 0 || request.FiscalYearId <= 0 ||
        request.TripDate == default || request.BusinessPartnerId <= 0 || !ValidCost(request.Cost) ||
        request.ActualDriverName?.Length > DriverTripRequest.MaximumActualDriverNameLength || request.Notes?.Length > DriverTripRequest.MaximumNotesLength
            ? InvalidRequest() : null;

    private static bool ValidCost(decimal? cost) => !cost.HasValue || (cost.Value >= 0m && cost.Value <= DriverTripRequest.MaximumCost && decimal.Round(cost.Value, 2) == cost.Value);
    private static Error? ValidateVersion(int id, byte[]? version) => id <= 0 ? InvalidRequest() : version is not { Length: 8 } ? RowVersionRequired() : null;
}
