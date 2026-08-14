using Mapster;
using static MiniErp.Application.Features.Countries.CountryErrors;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Countries;
using MiniErp.Domain.Entities.ReferenceData;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Countries;

public sealed class CountryService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService)
    : ICountryService, IScopedService
{
    public async Task<Result<PagedResponse<CountryResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CountryFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new CountryFilterRequest();
        var query = dbContext.Countries
            .AsNoTracking()
            .Where(country =>
                string.IsNullOrWhiteSpace(filters.Search) ||
                country.Code.Contains(filters.Search.Trim()) ||
                country.Name.Contains(filters.Search.Trim()) ||
                country.ArabicName.Contains(filters.Search.Trim()))
            .Where(country =>
                string.IsNullOrWhiteSpace(filters.Code) ||
                country.Code.Contains(filters.Code.Trim()))
            .Where(country =>
                string.IsNullOrWhiteSpace(filters.Name) ||
                country.Name.Contains(filters.Name.Trim()))
            .Where(country =>
                string.IsNullOrWhiteSpace(filters.ArabicName) ||
                country.ArabicName.Contains(filters.ArabicName.Trim()))
            .Where(country =>
                !filters.IsActive.HasValue ||
                country.IsActive == filters.IsActive.Value)
            .OrderBy(country => country.Name)
            .ThenBy(country => country.Id);

        return await paginationService.PaginateAsync<Country, CountryResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.Countries
            .AsNoTracking()
            .Where(country => country.IsActive)
            .OrderBy(country => country.Name)
            .ThenBy(country => country.Id)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<CountryResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CountryResponse>.Failure(InvalidId());
        }

        var response = await dbContext.Countries
            .AsNoTracking()
            .Where(country => country.Id == id)
            .ProjectToType<CountryResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<CountryResponse>.Failure(NotFound(id))
            : Result<CountryResponse>.Success(response);
    }

    public async Task<Result<CountryResponse>> AddAsync(
        CountryRequest request,
        CancellationToken cancellationToken = default)
    {
        var country = request.Adapt<Country>();
        country.Code = await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "CTR",
            companyId: null,
            existingIdentifiers: dbContext.Countries
                .IgnoreQueryFilters()
                .Select(entity => entity.Code),
            cancellationToken);

        dbContext.Countries.Add(country);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CountryResponse>.Success(country.Adapt<CountryResponse>());
    }

    public async Task<Result<CountryResponse>> UpdateAsync(
        int id,
        CountryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CountryResponse>.Failure(InvalidId());
        }

        var country = await dbContext.Countries.FirstOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken);
        if (country is null)
        {
            return Result<CountryResponse>.Failure(NotFound(id));
        }

        var code = country.Code;
        request.Adapt(country);
        country.Code = code;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CountryResponse>.Success(country.Adapt<CountryResponse>());
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var country = await dbContext.Countries.FirstOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken);
        if (country is null)
        {
            return Result.Failure(NotFound(id));
        }

        // Countries are global, so any company's current or historical
        // invoice must preserve the country reference.
        var hasInvoices = await dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(
                invoice => invoice.CountryId == id,
                cancellationToken);
        if (hasInvoices)
        {
            return Result.Failure(HasInvoices());
        }

        country.IsActive = false;
        dbContext.Countries.Remove(country);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

}
