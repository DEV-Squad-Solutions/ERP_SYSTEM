using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Companies;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Companies;

public sealed class CompanyService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentUserService currentUserService)
    : ICompanyService, IScopedService
{
    public async Task<Result<PagedResponse<CompanyResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CompanyFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new CompanyFilterRequest();
        var query = dbContext.Companies
            .AsNoTracking()
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.Search) ||
                company.Name.Contains(filters.Search.Trim()) ||
                company.Address.Contains(filters.Search.Trim()) ||
                company.CommercialRegister.Contains(filters.Search.Trim()) ||
                company.TaxNumber.Contains(filters.Search.Trim()) ||
                company.ManagerName.Contains(filters.Search.Trim()))
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.Name) ||
                company.Name.Contains(filters.Name.Trim()))
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.Address) ||
                company.Address.Contains(filters.Address.Trim()))
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.CommercialRegister) ||
                company.CommercialRegister.Contains(filters.CommercialRegister.Trim()))
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.TaxNumber) ||
                company.TaxNumber.Contains(filters.TaxNumber.Trim()))
            .Where(company =>
                string.IsNullOrWhiteSpace(filters.ManagerName) ||
                company.ManagerName.Contains(filters.ManagerName.Trim()))
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

        var currentUserResult = currentUserService.GetUserId();
        if (currentUserResult.IsFailure)
        {
            return Result<CompanyResponse>.Failure(currentUserResult.Error);
        }

        dbContext.Companies.Add(company);
        dbContext.UserCompanies.Add(new UserCompany
        {
            UserId = currentUserResult.Value,
            Company = company
        });
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
            await dbContext.BusinessPartners
                .IgnoreQueryFilters()
                .AnyAsync(partner => partner.CompanyId == id, cancellationToken) ||
            await dbContext.Drivers
                .IgnoreQueryFilters()
                .AnyAsync(driver => driver.CompanyId == id, cancellationToken) ||
            await dbContext.Stores
                .IgnoreQueryFilters()
                .AnyAsync(store => store.CompanyId == id, cancellationToken) ||
            await dbContext.Containers
                .IgnoreQueryFilters()
                .AnyAsync(container => container.CompanyId == id, cancellationToken) ||
            await dbContext.Cashboxes
                .IgnoreQueryFilters()
                .AnyAsync(cashbox => cashbox.CompanyId == id, cancellationToken) ||
            await dbContext.CashMovementTypes
                .IgnoreQueryFilters()
                .AnyAsync(
                    movementType => movementType.CompanyId == id,
                    cancellationToken) ||
            await dbContext.CashVouchers
                .IgnoreQueryFilters()
                .AnyAsync(voucher => voucher.CompanyId == id, cancellationToken) ||
            await dbContext.StoreContainers
                .IgnoreQueryFilters()
                .AnyAsync(
                    assignment => assignment.CompanyId == id,
                    cancellationToken);

        if (hasDependencies)
        {
            return Result.Failure(
                Error.Conflict(
                    "Companies.HasDependencies",
                    "لا يمكن حذف الشركة لوجود مستخدمين مرتبطين بها أو بيانات حالية أو تاريخية تخصها."));
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
                $"السجل التجاري '{commercialRegister}' مستخدم بالفعل.",
                nameof(CompanyRequest.CommercialRegister));
        }

        var taxNumberExists = await dbContext.Companies.AnyAsync(
            company =>
                (!excludedId.HasValue || company.Id != excludedId.Value) &&
                company.TaxNumber == taxNumber,
            cancellationToken);

        return taxNumberExists
            ? Error.Conflict(
                "Companies.TaxNumberExists",
                $"الرقم الضريبي '{taxNumber}' مستخدم بالفعل.",
                nameof(CompanyRequest.TaxNumber))
            : null;
    }

    private static Error InvalidId() =>
        Error.Validation("Companies.InvalidId", "يجب أن يكون رقم الشركة أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound("Companies.NotFound", $"لم يتم العثور على الشركة رقم {id}.");
}
