using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.BusinessPartners;
using MiniErp.Domain.Entities;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.BusinessPartners;

public sealed class BusinessPartnerService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IBusinessPartnerService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<BusinessPartnerResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner => partner.CompanyId == companyId)
            .OrderBy(partner => partner.Name)
            .ThenBy(partner => partner.Id);

        return await paginationService.PaginateAsync<
            BusinessPartner,
            BusinessPartnerResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner =>
                partner.CompanyId == companyId &&
                partner.IsActive)
            .OrderBy(partner => partner.Name)
            .ThenBy(partner => partner.Id)
            .ProjectToType<SelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SelectResponse>>.Success(response);
    }

    public async Task<Result<BusinessPartnerResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<BusinessPartnerResponse>.Failure(InvalidId());
        }

        var response = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner =>
                partner.Id == id &&
                partner.CompanyId == companyId)
            .ProjectToType<BusinessPartnerResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<BusinessPartnerResponse>.Failure(NotFound(id))
            : Result<BusinessPartnerResponse>.Success(response);
    }

    public async Task<Result<BusinessPartnerResponse>> AddAsync(
        BusinessPartnerRequest request,
        CancellationToken cancellationToken = default)
    {
        var partner = request.Adapt<BusinessPartner>();
        partner.CompanyId = companyId;

        var duplicateError = await FindDuplicateAsync(
            partner,
            excludedId: null,
            cancellationToken);
        if (duplicateError is not null)
        {
            return Result<BusinessPartnerResponse>.Failure(duplicateError);
        }

        dbContext.BusinessPartners.Add(partner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<BusinessPartnerResponse>.Success(
            partner.Adapt<BusinessPartnerResponse>());
    }

    public async Task<Result<BusinessPartnerResponse>> UpdateAsync(
        int id,
        BusinessPartnerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<BusinessPartnerResponse>.Failure(InvalidId());
        }

        var partner = await dbContext.BusinessPartners.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);
        if (partner is null)
        {
            return Result<BusinessPartnerResponse>.Failure(NotFound(id));
        }

        var normalizedPartner = request.Adapt<BusinessPartner>();
        var duplicateError = await FindDuplicateAsync(
            normalizedPartner,
            id,
            cancellationToken);
        if (duplicateError is not null)
        {
            return Result<BusinessPartnerResponse>.Failure(duplicateError);
        }

        request.Adapt(partner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<BusinessPartnerResponse>.Success(
            partner.Adapt<BusinessPartnerResponse>());
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var partner = await dbContext.BusinessPartners.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);
        if (partner is null)
        {
            return Result.Failure(NotFound(id));
        }

        partner.IsActive = false;
        dbContext.BusinessPartners.Remove(partner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Error?> FindDuplicateAsync(
        BusinessPartner partner,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var duplicates = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                (!excludedId.HasValue || entity.Id != excludedId.Value) &&
                (entity.Code == partner.Code ||
                 (partner.TaxNumber != null &&
                  entity.TaxNumber == partner.TaxNumber)))
            .Select(entity => new
            {
                entity.Code,
                entity.TaxNumber
            })
            .ToListAsync(cancellationToken);

        if (duplicates.Any(entity => string.Equals(
                entity.Code,
                partner.Code,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict(
                "BusinessPartners.CodeExists",
                $"Business partner code '{partner.Code}' already exists.");
        }

        return partner.TaxNumber is not null &&
               duplicates.Any(entity => string.Equals(
                   entity.TaxNumber,
                   partner.TaxNumber,
                   StringComparison.OrdinalIgnoreCase))
            ? Error.Conflict(
                "BusinessPartners.TaxNumberExists",
                "A business partner with the same tax number already exists.")
            : null;
    }

    private static Error InvalidId() =>
        Error.Validation(
            "BusinessPartners.InvalidId",
            "Business partner ID must be greater than zero.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "BusinessPartners.NotFound",
            $"Business partner with ID {id} was not found.");
}
