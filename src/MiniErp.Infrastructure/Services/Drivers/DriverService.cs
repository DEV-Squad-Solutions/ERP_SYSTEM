using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Drivers;
using MiniErp.Domain.Entities;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Drivers;

public sealed class DriverService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider)
    : IDriverService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<DriverResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Drivers
            .AsNoTracking()
            .Where(driver => driver.CompanyId == companyId)
            .OrderBy(driver => driver.Name)
            .ThenBy(driver => driver.Id);

        return await paginationService.PaginateAsync<Driver, DriverResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);
        var response = await dbContext.Drivers
            .AsNoTracking()
            .Where(driver =>
                driver.CompanyId == companyId &&
                driver.IsActive &&
                (driver.LicenseExpiryDate == null ||
                 driver.LicenseExpiryDate >= today))
            .OrderBy(driver => driver.Name)
            .ThenBy(driver => driver.Id)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<DriverResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<DriverResponse>.Failure(InvalidId());
        }

        var response = await dbContext.Drivers
            .AsNoTracking()
            .Where(driver => driver.Id == id && driver.CompanyId == companyId)
            .ProjectToType<DriverResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<DriverResponse>.Failure(NotFound(id))
            : Result<DriverResponse>.Success(response);
    }

    public async Task<Result<DriverResponse>> AddAsync(
        DriverRequest request,
        CancellationToken cancellationToken = default)
    {
        var driver = request.Adapt<Driver>();
        driver.CompanyId = companyId;

        var duplicateError = await FindDuplicateAsync(
            driver,
            excludedId: null,
            cancellationToken);
        if (duplicateError is not null)
        {
            return Result<DriverResponse>.Failure(duplicateError);
        }

        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<DriverResponse>.Success(driver.Adapt<DriverResponse>());
    }

    public async Task<Result<DriverResponse>> UpdateAsync(
        int id,
        DriverRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<DriverResponse>.Failure(InvalidId());
        }

        var driver = await dbContext.Drivers.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);
        if (driver is null)
        {
            return Result<DriverResponse>.Failure(NotFound(id));
        }

        var normalizedDriver = request.Adapt<Driver>();
        var duplicateError = await FindDuplicateAsync(
            normalizedDriver,
            id,
            cancellationToken);
        if (duplicateError is not null)
        {
            return Result<DriverResponse>.Failure(duplicateError);
        }

        request.Adapt(driver);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<DriverResponse>.Success(driver.Adapt<DriverResponse>());
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var driver = await dbContext.Drivers.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);
        if (driver is null)
        {
            return Result.Failure(NotFound(id));
        }

        driver.IsActive = false;
        dbContext.Drivers.Remove(driver);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Error?> FindDuplicateAsync(
        Driver driver,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var duplicates = await dbContext.Drivers
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                (!excludedId.HasValue || entity.Id != excludedId.Value) &&
                (entity.Name == driver.Name ||
                 entity.Code == driver.Code ||
                 entity.LicenseNumber == driver.LicenseNumber ||
                 (driver.NationalId != null &&
                  entity.NationalId == driver.NationalId)))
            .Select(entity => new
            {
                entity.Name,
                entity.Code,
                entity.LicenseNumber,
                entity.NationalId
            })
            .ToListAsync(cancellationToken);

        if (duplicates.Any(entity => string.Equals(
                entity.Name,
                driver.Name,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict(
                "Drivers.NameExists",
                $"Driver name '{driver.Name}' already exists.");
        }

        if (duplicates.Any(entity => string.Equals(
                entity.Code,
                driver.Code,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict(
                "Drivers.CodeExists",
                $"Driver code '{driver.Code}' already exists.");
        }

        if (duplicates.Any(entity => string.Equals(
                entity.LicenseNumber,
                driver.LicenseNumber,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict(
                "Drivers.LicenseNumberExists",
                $"Driver licence number '{driver.LicenseNumber}' already exists.");
        }

        return driver.NationalId is not null &&
               duplicates.Any(entity => string.Equals(
                   entity.NationalId,
                   driver.NationalId,
                   StringComparison.OrdinalIgnoreCase))
            ? Error.Conflict(
                "Drivers.NationalIdExists",
                "A driver with the same national ID already exists.")
            : null;
    }

    private static Error InvalidId() =>
        Error.Validation(
            "Drivers.InvalidId",
            "Driver ID must be greater than zero.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "Drivers.NotFound",
            $"Driver with ID {id} was not found.");
}
