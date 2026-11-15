using Mapster;
using System.Data;
using static MiniErp.Application.Features.DriverTrips.DriverTripErrors;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.DriverTrips;

public sealed partial class DriverTripService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
    IFiscalYearPeriodGuard fiscalYearPeriodGuard,
    IDriverTripPostingService? driverTripPostingService = null)
    : IDriverTripService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<DriverTripCostResponse>>>
        GetCostEntryAsync(
            PaginationRequest pagination,
            DriverTripCostFilterRequest? filters = null,
            CancellationToken cancellationToken = default)
    {
        filters ??= new DriverTripCostFilterRequest();
        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.FromDate,
            toDate: filters.ToDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<PagedResponse<DriverTripCostResponse>>.Failure(
                fiscalYear.Errors);
        }

        var search = filters.Search?.Trim();
        var invoiceNumber = filters.InvoiceNumber?.Trim();
        var tripNumber = filters.TripNumber?.Trim();
        var numericTripNumber = ParseTripNumber(tripNumber);
        var numericSearchTripNumber = ParseTripNumber(search);

        var query = dbContext.DriverTrips
            .AsNoTracking()
            .Where(trip =>
                trip.CompanyId == companyId &&
                trip.FiscalYearId == fiscalYear.Value.FiscalYearId)
            .Where(trip =>
                string.IsNullOrEmpty(search) ||
                (trip.InvoiceNumber != null && trip.InvoiceNumber.Contains(search)) ||
                (trip.ExportInvoiceCode != null &&
                 trip.ExportInvoiceCode.Contains(search)) ||
                trip.Driver.Code.Contains(search) ||
                trip.Driver.Name.Contains(search) ||
                (numericSearchTripNumber.HasValue &&
                 trip.Id == numericSearchTripNumber.Value))
            .Where(trip =>
                !filters.FromDate.HasValue ||
                trip.TripDate >= filters.FromDate.Value)
            .Where(trip =>
                !filters.ToDate.HasValue ||
                trip.TripDate <= filters.ToDate.Value)
            .Where(trip =>
                !filters.DriverId.HasValue ||
                trip.DriverId == filters.DriverId.Value)
            .Where(trip =>
                string.IsNullOrEmpty(invoiceNumber) ||
                (trip.InvoiceNumber != null && trip.InvoiceNumber.Contains(invoiceNumber)))
            .Where(trip => !filters.IsStandalone.HasValue || (trip.InvoiceId == null) == filters.IsStandalone.Value)
            .Where(trip =>
                string.IsNullOrEmpty(tripNumber) ||
                (numericTripNumber.HasValue &&
                 trip.Id == numericTripNumber.Value))
            .Where(trip =>
                !filters.HasCost.HasValue ||
                (trip.Cost.HasValue && trip.Cost.Value > 0m) ==
                filters.HasCost.Value);
        var orderedQuery = query
            .OrderByDescending(trip => trip.TripDate)
            .ThenByDescending(trip => trip.Id);

        return await paginationService.PaginateAsync<
            DriverTrip,
            DriverTripCostResponse>(
            orderedQuery,
            pagination,
            cancellationToken);
    }

    public async Task<Result<DriverTripBulkCostUpdateResponse>>
        UpdateCostsAsync(
            DriverTripBulkCostUpdateRequest request,
            CancellationToken cancellationToken = default)
    {
        if (request.Items is not { Count: > 0 } ||
            request.Items.Count >
            DriverTripBulkCostUpdateRequest.MaximumItemCount || request.Items.Any(item => item is null))
        {
            return Result<DriverTripBulkCostUpdateResponse>.Failure(
                InvalidItems());
        }

        var ids = request.Items
            .Select(item => item.DriverTripId)
            .Distinct()
            .ToArray();
        if (ids.Length != request.Items.Count)
        {
            return Result<DriverTripBulkCostUpdateResponse>.Failure(
                DuplicateIds());
        }

        if (request.Items.Any(item =>
                item.DriverTripId <= 0 ||
                !ValidCost(item.Cost) ||
                item.Notes?.Length > DriverTripRequest.MaximumNotesLength ||
                item.RowVersion is not { Length: 8 }))
        {
            return Result<DriverTripBulkCostUpdateResponse>.Failure(
                InvalidItems());
        }

        if (driverTripPostingService is null)
        {
            return Result<DriverTripBulkCostUpdateResponse>.Failure(PostingUnavailable());
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var committed = false;
        try
        {
            var trips = await dbContext.DriverTrips.AsNoTracking()
                .Where(trip => trip.CompanyId == companyId && ids.Contains(trip.Id))
                .ToListAsync(cancellationToken);
            if (trips.Count != ids.Length)
            {
                return Result<DriverTripBulkCostUpdateResponse>.Failure(TripsNotFound());
            }

            foreach (var tripScope in trips
                         .Select(trip => new { trip.FiscalYearId, trip.TripDate })
                         .Distinct())
            {
                var periodResult = await fiscalYearPeriodGuard.EnsureOpenForFiscalYearAsync(
                    tripScope.FiscalYearId,
                    tripScope.TripDate,
                    nameof(DriverTripCostResponse.TripDate),
                    cancellationToken);
                if (periodResult.IsFailure)
                {
                    return Result<DriverTripBulkCostUpdateResponse>.Failure(periodResult.Errors);
                }
            }

            var requestById = request.Items.ToDictionary(item => item.DriverTripId);
            var staleTrip = trips.Find(trip => !trip.RowVersion.AsSpan().SequenceEqual(requestById[trip.Id].RowVersion));
            if (staleTrip is not null)
            {
                return Result<DriverTripBulkCostUpdateResponse>.Failure(Concurrency(staleTrip.Id));
            }

            foreach (var trip in trips)
            {
                var requested = requestById[trip.Id];
                var trackedTrip = TrackForWrite(trip);
                trackedTrip.Cost = requested.Cost;
                trackedTrip.CostNotes = string.IsNullOrWhiteSpace(requested.Notes) ? null : requested.Notes.Trim();
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var trip in trips)
            {
                var postingResult = await driverTripPostingService.SynchronizeAsync(trip.Id, cancellationToken);
                if (postingResult.IsFailure)
                {
                    return Result<DriverTripBulkCostUpdateResponse>.Failure(postingResult.Errors);
                }
            }

            var response = await dbContext.DriverTrips.AsNoTracking()
                .Where(trip => trip.CompanyId == companyId && ids.Contains(trip.Id))
                .OrderBy(trip => trip.Id)
                .ProjectToType<DriverTripCostResponse>()
                .ToListAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return Result<DriverTripBulkCostUpdateResponse>.Success(new DriverTripBulkCostUpdateResponse(response));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var failedId = exception.Entries
                .Select(entry => (entry.Entity as DriverTrip)?.Id)
                .FirstOrDefault(id => id.HasValue);
            return Result<DriverTripBulkCostUpdateResponse>.Failure(Concurrency(failedId));
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            return Result<DriverTripBulkCostUpdateResponse>.Failure(Concurrency());
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            return Result<DriverTripBulkCostUpdateResponse>.Failure(Concurrency());
        }
        finally
        {
            if (!committed)
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    private static int? ParseTripNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith(
                "TR-",
                StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[3..];
        }

        return int.TryParse(normalized, out var id) && id > 0
            ? id
            : null;
    }

}
