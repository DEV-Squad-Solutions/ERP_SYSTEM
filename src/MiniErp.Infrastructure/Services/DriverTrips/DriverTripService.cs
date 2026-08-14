using Mapster;
using static MiniErp.Application.Features.DriverTrips.DriverTripErrors;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.DriverTrips;

public sealed class DriverTripService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
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
        var search = filters.Search?.Trim();
        var invoiceNumber = filters.InvoiceNumber?.Trim();
        var tripNumber = filters.TripNumber?.Trim();
        var numericTripNumber = ParseTripNumber(tripNumber);
        var numericSearchTripNumber = ParseTripNumber(search);

        var query = dbContext.DriverTrips
            .AsNoTracking()
            .Where(trip => trip.CompanyId == companyId)
            .Where(trip =>
                string.IsNullOrEmpty(search) ||
                trip.InvoiceNumber.Contains(search) ||
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
                trip.InvoiceNumber.Contains(invoiceNumber))
            .Where(trip =>
                string.IsNullOrEmpty(tripNumber) ||
                (numericTripNumber.HasValue &&
                 trip.Id == numericTripNumber.Value))
            .Where(trip =>
                !filters.HasCost.HasValue ||
                (trip.Cost.HasValue && trip.Cost.Value > 0m) ==
                filters.HasCost.Value)
            .OrderByDescending(trip => trip.TripDate)
            .ThenByDescending(trip => trip.Id);

        return await paginationService.PaginateAsync<
            DriverTrip,
            DriverTripCostResponse>(
            query,
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
            DriverTripBulkCostUpdateRequest.MaximumItemCount)
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
                item.Cost < 0m ||
                item.RowVersion is not { Length: 8 }))
        {
            return Result<DriverTripBulkCostUpdateResponse>.Failure(
                InvalidItems());
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var trips = await dbContext.DriverTrips
            .Where(trip =>
                trip.CompanyId == companyId &&
                ids.Contains(trip.Id))
            .ToListAsync(cancellationToken);
        if (trips.Count != ids.Length)
        {
            return Result<DriverTripBulkCostUpdateResponse>.Failure(
                TripsNotFound());
        }

        var requestById = request.Items.ToDictionary(
            item => item.DriverTripId);

        foreach (var trip in trips)
        {
            var requested = requestById[trip.Id];
            if (!trip.RowVersion.SequenceEqual(requested.RowVersion!))
            {
                return Result<DriverTripBulkCostUpdateResponse>.Failure(
                    Concurrency(trip.Id));
            }

            var entry = dbContext.Entry(trip);
            entry.Property(item => item.RowVersion).OriginalValue =
                requested.RowVersion!;
            trip.Cost = requested.Cost;
            trip.CostNotes = string.IsNullOrWhiteSpace(requested.Notes)
                ? null
                : requested.Notes.Trim();
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            var failedId = exception.Entries
                .Select(entry => (entry.Entity as DriverTrip)?.Id)
                .FirstOrDefault(id => id.HasValue) ?? 0;
            return Result<DriverTripBulkCostUpdateResponse>.Failure(
                Concurrency(failedId));
        }

        var response = await dbContext.DriverTrips
            .AsNoTracking()
            .Where(trip =>
                trip.CompanyId == companyId &&
                ids.Contains(trip.Id))
            .OrderBy(trip => trip.Id)
            .ProjectToType<DriverTripCostResponse>()
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<DriverTripBulkCostUpdateResponse>.Success(
            new DriverTripBulkCostUpdateResponse(response));
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
