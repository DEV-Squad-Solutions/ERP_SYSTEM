using Mapster;
using static MiniErp.Application.Features.Drivers.DriverErrors;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Drivers;
using MiniErp.Domain.Entities.Logistics;
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
        DriverFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new DriverFilterRequest();
        var today = DateOnly.FromDateTime(
            timeProvider.GetUtcNow().UtcDateTime);
        var query = dbContext.Drivers
            .AsNoTracking()
            .Where(driver => driver.CompanyId == companyId)
            .Where(driver =>
                string.IsNullOrWhiteSpace(filters.Search) ||
                driver.Code.Contains(filters.Search.Trim()) ||
                driver.Name.Contains(filters.Search.Trim()) ||
                driver.LicenseNumber.Contains(filters.Search.Trim()) ||
                (driver.PhoneNumber != null &&
                 driver.PhoneNumber.Contains(filters.Search.Trim())) ||
                (driver.NationalId != null &&
                 driver.NationalId.Contains(filters.Search.Trim())))
            .Where(driver =>
                string.IsNullOrWhiteSpace(filters.Code) ||
                driver.Code.Contains(filters.Code.Trim()))
            .Where(driver =>
                string.IsNullOrWhiteSpace(filters.Name) ||
                driver.Name.Contains(filters.Name.Trim()))
            .Where(driver =>
                string.IsNullOrWhiteSpace(filters.LicenseNumber) ||
                driver.LicenseNumber.Contains(filters.LicenseNumber.Trim()))
            .Where(driver =>
                !filters.IsActive.HasValue ||
                driver.IsActive == filters.IsActive.Value)
            .Where(driver =>
                !filters.HasExpiredLicense.HasValue ||
                (driver.LicenseExpiryDate.HasValue &&
                 driver.LicenseExpiryDate.Value < today) == filters.HasExpiredLicense.Value)
            .Where(driver =>
                !filters.LicenseExpiryFrom.HasValue ||
                (driver.LicenseExpiryDate.HasValue &&
                 driver.LicenseExpiryDate.Value >= filters.LicenseExpiryFrom.Value))
            .Where(driver =>
                !filters.LicenseExpiryTo.HasValue ||
                (driver.LicenseExpiryDate.HasValue &&
                 driver.LicenseExpiryDate.Value <= filters.LicenseExpiryTo.Value))
            .OrderByDescending(driver => driver.CreatedOn)
            .ThenByDescending(driver => driver.Id);

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
        driver.Code = await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "DRV",
            companyId: companyId,
            existingIdentifiers: dbContext.Drivers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.Code),
            cancellationToken);

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

        var code = driver.Code;
        request.Adapt(driver);
        driver.Code = code;
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

        var hasDependencies = await dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(
                invoice =>
                    invoice.CompanyId == companyId &&
                    invoice.DriverId == id,
                cancellationToken) ||
            await dbContext.DriverTrips
                .IgnoreQueryFilters()
                .AnyAsync(
                trip =>
                    trip.CompanyId == companyId &&
                    trip.DriverId == id,
                    cancellationToken) ||
            await dbContext.CashVouchers
                .IgnoreQueryFilters()
                .AnyAsync(
                    voucher =>
                        voucher.CompanyId == companyId &&
                        voucher.DriverId == id,
                    cancellationToken);
        if (hasDependencies)
        {
            return Result.Failure(HasDependencies());
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
                 entity.LicenseNumber == driver.LicenseNumber ||
                 (driver.NationalId != null &&
                  entity.NationalId == driver.NationalId)))
            .Select(entity => new
            {
                entity.Name,
                entity.LicenseNumber,
                entity.NationalId
            })
            .ToListAsync(cancellationToken);

        if (duplicates.Any(entity => string.Equals(
                entity.Name,
                driver.Name,
                StringComparison.OrdinalIgnoreCase)))
        {
            return NameExists(driver.Name);
        }

        if (duplicates.Any(entity => string.Equals(
                entity.LicenseNumber,
                driver.LicenseNumber,
                StringComparison.OrdinalIgnoreCase)))
        {
            return LicenseNumberExists(driver.LicenseNumber);
        }

        return driver.NationalId is not null &&
               duplicates.Any(entity => string.Equals(
                   entity.NationalId,
                   driver.NationalId,
                   StringComparison.OrdinalIgnoreCase))
            ? NationalIdExists()
            : null;
    }

}
