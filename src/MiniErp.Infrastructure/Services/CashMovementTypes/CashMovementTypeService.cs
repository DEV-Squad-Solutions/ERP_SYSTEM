using Mapster;
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

        dbContext.CashMovementTypes.Add(movementType);
        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await ProjectResponseQuery(movementType.Id)
            .FirstAsync(cancellationToken);
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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<CashMovementTypeResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);
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
            ? Error.Conflict(
                "CashMovementTypes.NameExists",
                $"نوع الحركة النقدية '{movementType.Name}' موجود بالفعل في نفس الاتجاه.",
                nameof(CashMovementTypeRequest.Name))
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

    private static Error InvalidId() =>
        Error.Validation(
            "CashMovementTypes.InvalidId",
            "يجب أن يكون رقم نوع الحركة النقدية أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "CashMovementTypes.NotFound",
            $"لم يتم العثور على نوع الحركة النقدية رقم {id}.");

    private static Error RowVersionRequired() =>
        Error.Validation(
            "CashMovementTypes.RowVersionRequired",
            "يجب إرسال إصدار نوع الحركة النقدية الحالي للتعديل.",
            nameof(CashMovementTypeUpdateRequest.RowVersion));

    private static Error Concurrency() =>
        Error.Conflict(
            "CashMovementTypes.Concurrency",
            "تم تعديل نوع الحركة النقدية بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    private static Error HasVouchers() =>
        Error.Conflict(
            "CashMovementTypes.HasVouchers",
            "لا يمكن حذف نوع الحركة النقدية لارتباطه بسندات حالية أو تاريخية. يمكن إلغاء تنشيطه بدلاً من ذلك.");

    private static Error UsedSemanticsChangeNotAllowed() =>
        Error.Conflict(
            "CashMovementTypes.UsedSemanticsChangeNotAllowed",
            "لا يمكن تغيير اتجاه أو أثر نوع الحركة بعد استخدامه في سند نقدية.");
}
