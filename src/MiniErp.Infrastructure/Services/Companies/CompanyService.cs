using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Companies;
using MiniErp.Domain.Entities;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Companies;

public sealed class CompanyService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService)
    : ICompanyService, IScopedService
{
    public async Task<Result<PagedResponse<CompanyResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Companies
            .AsNoTracking()
            .OrderBy(company => company.Name)
            .ThenBy(company => company.Id);

        return await paginationService.PaginateAsync<Company, CompanyResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.Companies
            .AsNoTracking()
            .OrderBy(company => company.Name)
            .ThenBy(company => company.Id)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<CompanyResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CompanyResponse>.Failure(InvalidId());
        }

        var response = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.Id == id)
            .ProjectToType<CompanyResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<CompanyResponse>.Failure(NotFound(id))
            : Result<CompanyResponse>.Success(response);
    }

    public async Task<Result<CompanyResponse>> AddAsync(
        CompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var company = request.Adapt<Company>();
        var duplicateError = await FindDuplicateAsync(
            company.CommercialRegister,
            company.TaxNumber,
            excludedId: null,
            cancellationToken);

        if (duplicateError is not null)
        {
            return Result<CompanyResponse>.Failure(duplicateError);
        }

        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CompanyResponse>.Success(company.Adapt<CompanyResponse>());
    }

    public async Task<Result<CompanyResponse>> UpdateAsync(
        int id,
        CompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CompanyResponse>.Failure(InvalidId());
        }

        var company = await dbContext.Companies.FirstOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken);

        if (company is null)
        {
            return Result<CompanyResponse>.Failure(NotFound(id));
        }

        var normalizedCompany = request.Adapt<Company>();
        var duplicateError = await FindDuplicateAsync(
            normalizedCompany.CommercialRegister,
            normalizedCompany.TaxNumber,
            id,
            cancellationToken);

        if (duplicateError is not null)
        {
            return Result<CompanyResponse>.Failure(duplicateError);
        }

        request.Adapt(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CompanyResponse>.Success(company.Adapt<CompanyResponse>());
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var company = await dbContext.Companies.FirstOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken);

        if (company is null)
        {
            return Result.Failure(NotFound(id));
        }

        var hasDependencies = await dbContext.UserCompanies.AnyAsync(
            userCompany => userCompany.CompanyId == id,
            cancellationToken) ||
            await dbContext.Items
                .IgnoreQueryFilters()
                .AnyAsync(item => item.CompanyId == id, cancellationToken) ||
            await dbContext.ItemUnits
                .IgnoreQueryFilters()
                .AnyAsync(itemUnit => itemUnit.CompanyId == id, cancellationToken) ||
            await dbContext.Stores
                .IgnoreQueryFilters()
                .AnyAsync(store => store.CompanyId == id, cancellationToken);

        if (hasDependencies)
        {
            return Result.Failure(
                Error.Conflict(
                    "Companies.HasDependencies",
                    "The company cannot be deleted because it has assigned users or current/historical business data."));
        }

        dbContext.Companies.Remove(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Error?> FindDuplicateAsync(
        string commercialRegister,
        string taxNumber,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var commercialRegisterExists = await dbContext.Companies.AnyAsync(
            company =>
                (!excludedId.HasValue || company.Id != excludedId.Value) &&
                company.CommercialRegister == commercialRegister,
            cancellationToken);

        if (commercialRegisterExists)
        {
            return Error.Conflict(
                "Companies.CommercialRegisterExists",
                $"Commercial register '{commercialRegister}' already exists.");
        }

        var taxNumberExists = await dbContext.Companies.AnyAsync(
            company =>
                (!excludedId.HasValue || company.Id != excludedId.Value) &&
                company.TaxNumber == taxNumber,
            cancellationToken);

        return taxNumberExists
            ? Error.Conflict(
                "Companies.TaxNumberExists",
                $"Tax number '{taxNumber}' already exists.")
            : null;
    }

    private static Error InvalidId() =>
        Error.Validation("Companies.InvalidId", "Company ID must be greater than zero.");

    private static Error NotFound(int id) =>
        Error.NotFound("Companies.NotFound", $"Company with ID {id} was not found.");
}
