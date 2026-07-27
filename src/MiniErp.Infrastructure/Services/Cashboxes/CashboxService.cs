using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Cashboxes;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Cashboxes;

public sealed class CashboxService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : ICashboxService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<CashboxResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CashboxFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new CashboxFilterRequest();
        var search = filters.Search?.Trim();
        var code = filters.Code?.Trim();
        var name = filters.Name?.Trim();

        var query = dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox => cashbox.CompanyId == companyId)
            .Where(cashbox =>
                string.IsNullOrEmpty(search) ||
                cashbox.Code.Contains(search) ||
                cashbox.Name.Contains(search) ||
                (cashbox.Notes != null &&
                 cashbox.Notes.Contains(search)))
            .Where(cashbox =>
                string.IsNullOrEmpty(code) ||
                cashbox.Code.Contains(code))
            .Where(cashbox =>
                string.IsNullOrEmpty(name) ||
                cashbox.Name.Contains(name))
            .Where(cashbox =>
                !filters.Currency.HasValue ||
                cashbox.Currency == filters.Currency.Value)
            .Where(cashbox =>
                !filters.IsActive.HasValue ||
                cashbox.IsActive == filters.IsActive.Value)
            .OrderBy(cashbox => cashbox.Name)
            .ThenBy(cashbox => cashbox.Id);

        return await paginationService.PaginateAsync<
            Cashbox,
            CashboxResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<CashboxSelectResponse>>>
        GetSelectAsync(CancellationToken cancellationToken = default)
    {
        var response = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.IsActive)
            .OrderBy(cashbox => cashbox.Name)
            .ThenBy(cashbox => cashbox.Id)
            .ProjectToType<CashboxSelectResponse>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CashboxSelectResponse>>.Success(response);
    }

    public async Task<Result<CashboxResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashboxResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<CashboxResponse>.Failure(NotFound(id))
            : Result<CashboxResponse>.Success(response);
    }

    public async Task<Result<CashboxResponse>> AddAsync(
        CashboxRequest request,
        CancellationToken cancellationToken = default)
    {
        var cashbox = request.Adapt<Cashbox>();
        cashbox.CompanyId = companyId;

        var duplicateError = await FindDuplicateAsync(
            cashbox,
            excludedId: null,
            cancellationToken);
        if (duplicateError is not null)
        {
            return Result<CashboxResponse>.Failure(duplicateError);
        }

        dbContext.Cashboxes.Add(cashbox);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(cashbox.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        return Result<CashboxResponse>.Success(response);
    }

    public async Task<Result<CashboxResponse>> UpdateAsync(
        int id,
        CashboxUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashboxResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<CashboxResponse>.Failure(RowVersionRequired());
        }

        var cashbox = await dbContext.Cashboxes.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (cashbox is null)
        {
            return Result<CashboxResponse>.Failure(NotFound(id));
        }

        if (!cashbox.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<CashboxResponse>.Failure(Concurrency());
        }

        var normalized = request.Adapt<Cashbox>();
        var duplicateError = await FindDuplicateAsync(
            normalized,
            id,
            cancellationToken);
        if (duplicateError is not null)
        {
            return Result<CashboxResponse>.Failure(duplicateError);
        }

        var hasVouchers = await HasVouchersAsync(id, cancellationToken);
        if (hasVouchers &&
            (cashbox.OpeningBalance != request.OpeningBalance ||
             cashbox.Currency != request.Currency))
        {
            return Result<CashboxResponse>.Failure(
                OpeningOrCurrencyChangeNotAllowed());
        }

        var entry = dbContext.Entry(cashbox);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;
        request.Adapt(cashbox);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<CashboxResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        return Result<CashboxResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var cashbox = await dbContext.Cashboxes.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (cashbox is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (await HasVouchersAsync(id, cancellationToken))
        {
            return Result.Failure(HasVouchers());
        }

        cashbox.IsActive = false;
        dbContext.Cashboxes.Remove(cashbox);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<CashboxResponse> ProjectResponseQuery(int id) =>
        dbContext.Cashboxes
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.Id == id)
            .ProjectToType<CashboxResponse>();

    private async Task<Error?> FindDuplicateAsync(
        Cashbox cashbox,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var normalizedCode = cashbox.Code.ToUpperInvariant();
        var normalizedName = cashbox.Name.ToUpperInvariant();

        var duplicate = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                (!excludedId.HasValue || entity.Id != excludedId.Value) &&
                (entity.Code.ToUpper() == normalizedCode ||
                 entity.Name.ToUpper() == normalizedName))
            .Select(entity => new
            {
                entity.Code,
                entity.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (duplicate is null)
        {
            return null;
        }

        return string.Equals(
                duplicate.Code,
                cashbox.Code,
                StringComparison.OrdinalIgnoreCase)
            ? Error.Conflict(
                "Cashboxes.CodeExists",
                $"كود صندوق النقدية '{cashbox.Code}' مستخدم بالفعل.",
                nameof(CashboxRequest.Code))
            : Error.Conflict(
                "Cashboxes.NameExists",
                $"اسم صندوق النقدية '{cashbox.Name}' مستخدم بالفعل.",
                nameof(CashboxRequest.Name));
    }

    private async Task<bool> HasVouchersAsync(
        int cashboxId,
        CancellationToken cancellationToken) =>
        await dbContext.CashVouchers
            .IgnoreQueryFilters()
            .AnyAsync(
                voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.CashboxId == cashboxId,
                cancellationToken);

    private static Error InvalidId() =>
        Error.Validation(
            "Cashboxes.InvalidId",
            "يجب أن يكون رقم صندوق النقدية أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "Cashboxes.NotFound",
            $"لم يتم العثور على صندوق النقدية رقم {id}.");

    private static Error RowVersionRequired() =>
        Error.Validation(
            "Cashboxes.RowVersionRequired",
            "يجب إرسال إصدار صندوق النقدية الحالي للتعديل.",
            nameof(CashboxUpdateRequest.RowVersion));

    private static Error Concurrency() =>
        Error.Conflict(
            "Cashboxes.Concurrency",
            "تم تعديل صندوق النقدية بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");

    private static Error HasVouchers() =>
        Error.Conflict(
            "Cashboxes.HasVouchers",
            "لا يمكن حذف صندوق النقدية لارتباطه بسندات نقدية حالية أو تاريخية. يمكن إلغاء تنشيطه بدلاً من ذلك.");

    private static Error OpeningOrCurrencyChangeNotAllowed() =>
        Error.Conflict(
            "Cashboxes.OpeningOrCurrencyChangeNotAllowed",
            "لا يمكن تغيير الرصيد الافتتاحي أو العملة بعد إنشاء سندات على صندوق النقدية.");
}
