using System.Data;
using Mapster;
using static MiniErp.Application.Features.CashMovementTypes.CashMovementTypeErrors;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashMovementTypes;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.CashMovementTypes;

public sealed class CashMovementTypeService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : ICashMovementTypeService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<CashMovementTypeResponse>>>
        GetAllAsync(
            PaginationRequest pagination,
            CashMovementTypeFilterRequest? filters = null,
            CancellationToken cancellationToken = default)
    {
        filters ??= new CashMovementTypeFilterRequest();
        var search = filters.Search?.Trim();
        var name = filters.Name?.Trim();

        var query = dbContext.CashMovementTypes
            .AsNoTracking()
            .Where(movementType =>
                movementType.CompanyId == companyId)
            .Where(movementType =>
                string.IsNullOrEmpty(search) ||
                movementType.Name.Contains(search) ||
                (movementType.Notes != null &&
                 movementType.Notes.Contains(search)))
            .Where(movementType =>
                string.IsNullOrEmpty(name) ||
                movementType.Name.Contains(name))
            .Where(movementType =>
                !filters.Direction.HasValue ||
                movementType.Direction == filters.Direction.Value)
            .Where(movementType =>
                !filters.ForPartner.HasValue ||
                (movementType.PartnerEffect != PartnerAccountEffect.None) ==
                filters.ForPartner.Value)
            .Where(movementType =>
                !filters.IsActive.HasValue ||
                movementType.IsActive == filters.IsActive.Value)
            .OrderBy(movementType => movementType.Direction)
            .ThenBy(movementType => movementType.Name)
            .ThenBy(movementType => movementType.Id);

        return await paginationService.PaginateAsync<
            CashMovementType,
            CashMovementTypeResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<CashMovementTypeSelectResponse>>>
        GetSelectAsync(
            CashMovementTypeSelectRequest? filters = null,
            CancellationToken cancellationToken = default)
    {
        filters ??= new CashMovementTypeSelectRequest();

        var response = await dbContext.CashMovementTypes
            .AsNoTracking()
            .Where(movementType =>
                movementType.CompanyId == companyId &&
                movementType.IsActive)
            .Where(movementType =>
                !filters.Direction.HasValue ||
                movementType.Direction == filters.Direction.Value)
            .Where(movementType =>
                !filters.ForPartner.HasValue ||
                (filters.ForPartner.Value
                    ? movementType.PartnerEffect !=
                      PartnerAccountEffect.None
                    : movementType.PartnerEffect ==
                      PartnerAccountEffect.None))
            .OrderBy(movementType => movementType.Direction)
            .ThenBy(movementType => movementType.Name)
            .ThenBy(movementType => movementType.Id)
            .ProjectToType<CashMovementTypeSelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CashMovementTypeSelectResponse>>.Success(
            response);
    }

    public async Task<Result<CashMovementTypeResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashMovementTypeResponse>.Failure(InvalidId());
        }

        var response = await dbContext.CashMovementTypes
            .AsNoTracking()
            .Where(movementType =>
                movementType.CompanyId == companyId &&
                movementType.Id == id)
            .ProjectToType<CashMovementTypeResponse>()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<CashMovementTypeResponse>.Failure(NotFound(id))
            : Result<CashMovementTypeResponse>.Success(response);
    }

    public async Task<Result<CashMovementTypeResponse>> AddAsync(
        CashMovementTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var movementType = request.Adapt<CashMovementType>();
        movementType.CompanyId = companyId;

        var duplicateError = await FindDuplicateAsync(
            movementType,
            excludedId: null,
            cancellationToken);
        if (duplicateError is not null)
        {
            return Result<CashMovementTypeResponse>.Failure(duplicateError);
        }

        foreach (var invoiceType in GetDefaultInvoiceTypes(movementType))
        {
            await ClearExistingDefaultAsync(
                invoiceType,
                excludedId: null,
                cancellationToken);
        }

        dbContext.CashMovementTypes.Add(movementType);
        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await ProjectResponseQuery(movementType.Id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<CashMovementTypeResponse>.Success(response);
    }

    public async Task<Result<CashMovementTypeResponse>> UpdateAsync(
        int id,
        CashMovementTypeUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashMovementTypeResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<CashMovementTypeResponse>.Failure(
                RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var movementType = await dbContext.CashMovementTypes
            .FirstOrDefaultAsync(
                entity =>
                    entity.Id == id &&
                    entity.CompanyId == companyId,
                cancellationToken);
        if (movementType is null)
        {
            return Result<CashMovementTypeResponse>.Failure(NotFound(id));
        }

        if (!movementType.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<CashMovementTypeResponse>.Failure(Concurrency());
        }

        var normalized = request.Adapt<CashMovementType>();
        var duplicateError = await FindDuplicateAsync(
            normalized,
            id,
            cancellationToken);
        if (duplicateError is not null)
        {
            return Result<CashMovementTypeResponse>.Failure(duplicateError);
        }

        if ((movementType.Direction != request.Direction ||
             (movementType.PartnerEffect != PartnerAccountEffect.None) !=
             request.ForPartner) &&
            await HasVouchersAsync(id, cancellationToken))
        {
            return Result<CashMovementTypeResponse>.Failure(
                UsedSemanticsChangeNotAllowed());
        }

        var entry = dbContext.Entry(movementType);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;
        request.Adapt(movementType);

        foreach (var invoiceType in GetDefaultInvoiceTypes(movementType))
        {
            await ClearExistingDefaultAsync(
                invoiceType,
                excludedId: id,
                cancellationToken);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<CashMovementTypeResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<CashMovementTypeResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var movementType = await dbContext.CashMovementTypes
            .FirstOrDefaultAsync(
                entity =>
                    entity.Id == id &&
                    entity.CompanyId == companyId,
                cancellationToken);
        if (movementType is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (await HasVouchersAsync(id, cancellationToken))
        {
            return Result.Failure(HasVouchers());
        }

        movementType.IsActive = false;
        dbContext.CashMovementTypes.Remove(movementType);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Error?> FindDuplicateAsync(
        CashMovementType movementType,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var normalizedName = movementType.Name.ToUpperInvariant();
        var exists = await dbContext.CashMovementTypes
            .AsNoTracking()
            .AnyAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Direction == movementType.Direction &&
                    (!excludedId.HasValue ||
                     entity.Id != excludedId.Value) &&
                    entity.Name.ToUpper() == normalizedName,
                cancellationToken);

        return exists
            ? NameExists(movementType.Name)
            : null;
    }

    private IQueryable<CashMovementTypeResponse> ProjectResponseQuery(
        int id) =>
        dbContext.CashMovementTypes
            .AsNoTracking()
            .Where(movementType =>
                movementType.CompanyId == companyId &&
                movementType.Id == id)
            .ProjectToType<CashMovementTypeResponse>();

    private async Task<bool> HasVouchersAsync(
        int cashMovementTypeId,
        CancellationToken cancellationToken) =>
        await dbContext.CashVouchers
            .IgnoreQueryFilters()
            .AnyAsync(
                voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.CashMovementTypeId == cashMovementTypeId,
                cancellationToken);

    private async Task ClearExistingDefaultAsync(
        InvoiceType invoiceType,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CashMovementTypes.Where(entity =>
            entity.CompanyId == companyId &&
            (!excludedId.HasValue || entity.Id != excludedId.Value));

        _ = invoiceType switch
        {
            InvoiceType.Sales => await query
                .Where(entity => entity.IsDefaultForSales)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        entity => entity.IsDefaultForSales,
                        false),
                    cancellationToken),
            InvoiceType.Purchase => await query
                .Where(entity => entity.IsDefaultForPurchase)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        entity => entity.IsDefaultForPurchase,
                        false),
                    cancellationToken),
            InvoiceType.SalesReturn => await query
                .Where(entity => entity.IsDefaultForSalesReturn)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        entity => entity.IsDefaultForSalesReturn,
                        false),
                    cancellationToken),
            InvoiceType.PurchaseReturn => await query
                .Where(entity => entity.IsDefaultForPurchaseReturn)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        entity => entity.IsDefaultForPurchaseReturn,
                        false),
                    cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(invoiceType),
                invoiceType,
                "Unsupported invoice type.")
        };
    }

    private static IEnumerable<InvoiceType> GetDefaultInvoiceTypes(
        CashMovementType movementType)
    {
        if (movementType.IsDefaultForSales)
        {
            yield return InvoiceType.Sales;
        }

        if (movementType.IsDefaultForPurchase)
        {
            yield return InvoiceType.Purchase;
        }

        if (movementType.IsDefaultForSalesReturn)
        {
            yield return InvoiceType.SalesReturn;
        }

        if (movementType.IsDefaultForPurchaseReturn)
        {
            yield return InvoiceType.PurchaseReturn;
        }
    }
}
