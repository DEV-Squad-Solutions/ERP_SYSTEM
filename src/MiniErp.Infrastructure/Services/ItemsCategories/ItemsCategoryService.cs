using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ItemsCategories;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.ItemsCategories;

public sealed class ItemsCategoryService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IItemsCategoryService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<ItemsCategoryResponse>>> GetAllAsync(
        PaginationRequest pagination,
        ItemsCategoryFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new ItemsCategoryFilterRequest();
        var search = filters.Search?.Trim();
        var name = filters.Name?.Trim();

        var query = dbContext.ItemsCategories
            .AsNoTracking()
            .Where(category => category.CompanyId == companyId)
            .Where(category =>
                string.IsNullOrEmpty(search) ||
                category.Name.Contains(search) ||
                (category.Notes != null && category.Notes.Contains(search)))
            .Where(category =>
                string.IsNullOrEmpty(name) ||
                category.Name.Contains(name))
            .Where(category =>
                !filters.IsActive.HasValue ||
                category.IsActive == filters.IsActive.Value)
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id);

        return await paginationService.PaginateAsync<
            ItemsCategory,
            ItemsCategoryResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<ItemsCategorySelectResponse>>>
        GetSelectAsync(CancellationToken cancellationToken = default)
    {
        var response = await dbContext.ItemsCategories
            .AsNoTracking()
            .Where(category =>
                category.CompanyId == companyId &&
                category.IsActive)
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .ProjectToType<ItemsCategorySelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ItemsCategorySelectResponse>>.Success(
            response);
    }

    public async Task<Result<ItemsCategoryResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ItemsCategoryResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<ItemsCategoryResponse>.Failure(NotFound(id))
            : Result<ItemsCategoryResponse>.Success(response);
    }

    public async Task<Result<ItemsCategoryResponse>> AddAsync(
        ItemsCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = request.Adapt<ItemsCategory>();
        category.CompanyId = companyId;

        if (await NameExistsAsync(
                category.Name,
                category.IsActive,
                excludedId: null,
                cancellationToken))
        {
            return Result<ItemsCategoryResponse>.Failure(
                NameExists(category.Name));
        }

        dbContext.ItemsCategories.Add(category);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<ItemsCategoryResponse>.Failure(
                NameExists(category.Name));
        }

        var response = await ProjectResponseQuery(category.Id)
            .FirstAsync(cancellationToken);
        return Result<ItemsCategoryResponse>.Success(response);
    }

    public async Task<Result<ItemsCategoryResponse>> UpdateAsync(
        int id,
        ItemsCategoryUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ItemsCategoryResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<ItemsCategoryResponse>.Failure(
                RowVersionRequired());
        }

        var category = await dbContext.ItemsCategories
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.Id == id,
                cancellationToken);
        if (category is null)
        {
            return Result<ItemsCategoryResponse>.Failure(NotFound(id));
        }

        if (!category.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<ItemsCategoryResponse>.Failure(Concurrency());
        }

        var normalizedName = request.Name.Trim();
        if (await NameExistsAsync(
                normalizedName,
                request.IsActive,
                id,
                cancellationToken))
        {
            return Result<ItemsCategoryResponse>.Failure(
                NameExists(normalizedName));
        }

        var entry = dbContext.Entry(category);
        entry.Property(candidate => candidate.RowVersion).OriginalValue =
            request.RowVersion;
        request.Adapt(category);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<ItemsCategoryResponse>.Failure(Concurrency());
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<ItemsCategoryResponse>.Failure(
                NameExists(normalizedName));
        }

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);
        return Result<ItemsCategoryResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var category = await dbContext.ItemsCategories
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.CompanyId == companyId &&
                    candidate.Id == id,
                cancellationToken);
        if (category is null)
        {
            return Result.Failure(NotFound(id));
        }

        var hasInvoices = await dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(
                invoice =>
                    invoice.CompanyId == companyId &&
                    invoice.ItemsCategoryId == id,
                cancellationToken);
        if (hasInvoices)
        {
            return Result.Failure(HasInvoices());
        }

        category.IsActive = false;
        dbContext.ItemsCategories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private Task<bool> NameExistsAsync(
        string name,
        bool isActive,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        if (!isActive)
        {
            return Task.FromResult(false);
        }

        var normalizedName = name.ToUpperInvariant();
        return dbContext.ItemsCategories
            .AsNoTracking()
            .AnyAsync(
                category =>
                    category.CompanyId == companyId &&
                    category.IsActive &&
                    (!excludedId.HasValue ||
                     category.Id != excludedId.Value) &&
                    category.Name.ToUpper() == normalizedName,
                cancellationToken);
    }

    private IQueryable<ItemsCategoryResponse> ProjectResponseQuery(int id) =>
        dbContext.ItemsCategories
            .AsNoTracking()
            .Where(category =>
                category.CompanyId == companyId &&
                category.Id == id)
            .ProjectToType<ItemsCategoryResponse>();

    private static Error InvalidId() =>
        Error.Validation(
            "ItemsCategories.InvalidId",
            "يجب أن يكون رقم تصنيف الأصناف أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "ItemsCategories.NotFound",
            $"لم يتم العثور على تصنيف الأصناف رقم {id}.");

    private static Error NameExists(string name) =>
        Error.Conflict(
            "ItemsCategories.NameExists",
            $"تصنيف الأصناف النشط '{name}' موجود بالفعل.",
            nameof(ItemsCategoryRequest.Name));

    private static Error RowVersionRequired() =>
        Error.Validation(
            "ItemsCategories.RowVersionRequired",
            "يجب إرسال إصدار تصنيف الأصناف الحالي للتعديل.",
            nameof(ItemsCategoryUpdateRequest.RowVersion));

    private static Error Concurrency() =>
        Error.Conflict(
            "ItemsCategories.Concurrency",
            "تم تعديل تصنيف الأصناف بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    private static Error HasInvoices() =>
        Error.Conflict(
            "ItemsCategories.HasInvoices",
            "لا يمكن حذف تصنيف الأصناف لارتباطه بفواتير حالية أو تاريخية. يمكن إلغاء تنشيطه بدلاً من ذلك.");
}
