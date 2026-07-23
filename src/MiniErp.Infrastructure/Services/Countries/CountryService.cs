using Mapster;
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
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Countries
            .AsNoTracking()
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

        if (await ActiveCodeExistsAsync(
                country,
                excludedId: null,
                cancellationToken))
        {
            return Result<CountryResponse>.Failure(CodeExists(country.Code));
        }

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

        var normalizedCountry = request.Adapt<Country>();
        if (await ActiveCodeExistsAsync(
                normalizedCountry,
                id,
                cancellationToken))
        {
            return Result<CountryResponse>.Failure(
                CodeExists(normalizedCountry.Code));
        }

        request.Adapt(country);
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

        country.IsActive = false;
        dbContext.Countries.Remove(country);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private Task<bool> ActiveCodeExistsAsync(
        Country country,
        int? excludedId,
        CancellationToken cancellationToken) =>
        country.IsActive
            ? dbContext.Countries.AsNoTracking().AnyAsync(
                entity =>
                    entity.IsActive &&
                    entity.Code == country.Code &&
                    (!excludedId.HasValue || entity.Id != excludedId.Value),
                cancellationToken)
            : Task.FromResult(false);

    private static Error InvalidId() =>
        Error.Validation(
            "Countries.InvalidId",
            "يجب أن يكون رقم الدولة أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "Countries.NotFound",
            $"لم يتم العثور على الدولة رقم {id}.");

    private static Error CodeExists(string code) =>
        Error.Conflict(
            "Countries.CodeExists",
            $"كود الدولة '{code}' مستخدم بالفعل في دولة نشطة.",
            nameof(CountryRequest.Code));
}
