using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.DriverTrips;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.DriverTrips;

public sealed class DriverTripPostingService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IAccountMappingResolver accountMappingResolver,
    IAutomaticPostingService automaticPostingService)
    : IDriverTripPostingService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result> SynchronizeAsync(
        int driverTripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await dbContext.DriverTrips
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == driverTripId)
            .Select(entity => new
            {
                entity.Id,
                entity.InvoiceNumber,
                entity.TripDate,
                entity.Cost
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (trip is null)
        {
            return Result.Failure(DriverTripErrors.TripsNotFound());
        }

        if (trip.Cost is null or <= 0m)
        {
            return await DeleteAsync(trip.Id, cancellationToken);
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.StartDate <= trip.TripDate &&
                year.EndDate >= trip.TripDate)
            .Select(year => new
            {
                year.Id,
                year.Name,
                year.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            return Result.Failure(
                FiscalYearErrors.DateNotCovered(
                    trip.TripDate,
                    nameof(trip.TripDate)));
        }

        if (fiscalYear.Status != FiscalYearStatus.Open)
        {
            return Result.Failure(
                FiscalYearErrors.Closed(
                    trip.TripDate,
                    fiscalYear.Name,
                    nameof(trip.TripDate)));
        }

        var expenseResult = await accountMappingResolver.ResolveAsync(
            fiscalYear.Id,
            AccountingMappingType.DriverTripExpense,
            cancellationToken: cancellationToken);
        if (expenseResult.IsFailure)
        {
            return Result.Failure(expenseResult.Errors);
        }

        var driverResult = await accountMappingResolver.ResolveAsync(
            fiscalYear.Id,
            AccountingMappingType.DriverControl,
            cancellationToken: cancellationToken);
        if (driverResult.IsFailure)
        {
            return Result.Failure(driverResult.Errors);
        }

        var postingResult = await automaticPostingService.CreateOrUpdateAsync(
            new AutomaticJournalEntryRequest(
                FiscalYearId: fiscalYear.Id,
                EntryDate: trip.TripDate,
                Description: $"تكلفة رحلة الفاتورة {trip.InvoiceNumber}",
                SourceType: JournalEntrySourceType.DriverTrip,
                SourceId: trip.Id,
                SourceNumber: $"TR-{trip.Id}",
                Lines:
                [
                    new JournalEntryLineRequest(
                        expenseResult.Value,
                        "مصروف رحلة سائق",
                        trip.Cost.Value,
                        0m),
                    new JournalEntryLineRequest(
                        driverResult.Value,
                        "مستحقات السائق عن الرحلة",
                        0m,
                        trip.Cost.Value)
                ]),
            cancellationToken);
        return postingResult.IsFailure
            ? Result.Failure(postingResult.Errors)
            : Result.Success();
    }

    public Task<Result> DeleteAsync(
        int driverTripId,
        CancellationToken cancellationToken = default) =>
        automaticPostingService.DeleteAsync(
            JournalEntrySourceType.DriverTrip,
            driverTripId,
            cancellationToken);
}
