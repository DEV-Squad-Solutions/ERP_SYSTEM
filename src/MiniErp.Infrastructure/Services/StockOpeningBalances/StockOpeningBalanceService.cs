using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockOpeningBalances;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.StockOpeningBalances;

public sealed class StockOpeningBalanceService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IStockOpeningBalanceService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<StockOpeningBalanceListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.StockOpeningBalances
            .AsNoTracking()
            .Where(balance => balance.CompanyId == companyId)
            .OrderByDescending(balance => balance.DocumentDate)
            .ThenByDescending(balance => balance.Id);

        return await paginationService.PaginateAsync<
            StockOpeningBalance,
            StockOpeningBalanceListResponse>(
                query,
                pagination,
                cancellationToken);
    }

    public async Task<Result<StockOpeningBalanceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockOpeningBalanceResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<StockOpeningBalanceResponse>.Failure(NotFound(id))
            : Result<StockOpeningBalanceResponse>.Success(response);
    }

    public async Task<Result<StockOpeningBalanceResponse>> AddAsync(
        StockOpeningBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = request.Adapt<StockOpeningBalance>();

        var validationResult = await ValidateRequestAsync(
            normalized.StoreId,
            request.Lines,
            cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result<StockOpeningBalanceResponse>.Failure(
                validationResult.Error);
        }

        var documentNumberExists = await dbContext.StockOpeningBalances.AnyAsync(
            balance =>
                balance.CompanyId == companyId &&
                balance.DocumentNumber == normalized.DocumentNumber,
            cancellationToken);
        if (documentNumberExists)
        {
            return Result<StockOpeningBalanceResponse>.Failure(
                DocumentNumberExists(normalized.DocumentNumber));
        }

        var openingBalance = new StockOpeningBalance
        {
            CompanyId = companyId,
            StoreId = normalized.StoreId,
            DocumentNumber = normalized.DocumentNumber,
            DocumentDate = normalized.DocumentDate,
            Notes = normalized.Notes
        };

        AddLines(openingBalance, request.Lines, validationResult.Value);
        dbContext.StockOpeningBalances.Add(openingBalance);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(openingBalance.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        return Result<StockOpeningBalanceResponse>.Success(response);
    }

    public async Task<Result<StockOpeningBalanceResponse>> UpdateAsync(
        int id,
        StockOpeningBalanceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockOpeningBalanceResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: > 0 })
        {
            return Result<StockOpeningBalanceResponse>.Failure(
                Error.Validation(
                    "StockOpeningBalances.RowVersionRequired",
                    "يجب إرسال إصدار السجل الحالي للتعديل.",
                    nameof(StockOpeningBalanceUpdateRequest.RowVersion)));
        }

        var normalized = request.Adapt<StockOpeningBalance>();

        var openingBalance = await LoadForWriteAsync(id, cancellationToken);
        if (openingBalance is null)
        {
            return Result<StockOpeningBalanceResponse>.Failure(NotFound(id));
        }

        if (!openingBalance.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<StockOpeningBalanceResponse>.Failure(Concurrency());
        }

        var validationResult = await ValidateRequestAsync(
            normalized.StoreId,
            request.Lines,
            cancellationToken);
        if (validationResult.IsFailure)
        {
            return Result<StockOpeningBalanceResponse>.Failure(
                validationResult.Error);
        }

        var documentNumberExists = await dbContext.StockOpeningBalances.AnyAsync(
            balance =>
                balance.CompanyId == companyId &&
                balance.Id != id &&
                balance.DocumentNumber == normalized.DocumentNumber,
            cancellationToken);
        if (documentNumberExists)
        {
            return Result<StockOpeningBalanceResponse>.Failure(
                DocumentNumberExists(normalized.DocumentNumber));
        }

        openingBalance.StoreId = normalized.StoreId;
        openingBalance.DocumentNumber = normalized.DocumentNumber;
        openingBalance.DocumentDate = normalized.DocumentDate;
        openingBalance.Notes = normalized.Notes;

        ReplaceLines(
            openingBalance,
            request.Lines,
            validationResult.Value,
            dbContext);

        var openingBalanceEntry = dbContext.Entry(openingBalance);
        openingBalanceEntry.State = EntityState.Modified;
        openingBalanceEntry.Property(balance => balance.RowVersion)
            .OriginalValue = request.RowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<StockOpeningBalanceResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        return Result<StockOpeningBalanceResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var openingBalance = await LoadForWriteAsync(id, cancellationToken);
        if (openingBalance is null)
        {
            return Result.Failure(NotFound(id));
        }

        dbContext.StockOpeningBalanceLines.RemoveRange(openingBalance.Lines);
        dbContext.StockOpeningBalances.Remove(openingBalance);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        return Result.Success();
    }

    private IQueryable<StockOpeningBalanceResponse> ProjectResponseQuery(int id) =>
        dbContext.StockOpeningBalances
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.Id == id)
            .ProjectToType<StockOpeningBalanceResponse>();

    private Task<StockOpeningBalance?> LoadForWriteAsync(
        int id,
        CancellationToken cancellationToken) =>
        dbContext.StockOpeningBalances
            .Include(balance => balance.Lines.Where(line =>
                line.CompanyId == companyId))
            .FirstOrDefaultAsync(
                balance => balance.CompanyId == companyId && balance.Id == id,
                cancellationToken);

    private static void AddLines(
        StockOpeningBalance openingBalance,
        IReadOnlyList<StockOpeningBalanceLineRequest> requests,
        IReadOnlyDictionary<int, ItemSnapshot> items)
    {
        foreach (var lineRequest in requests)
        {
            var item = items[lineRequest.ItemId];
            var line = lineRequest.Adapt<StockOpeningBalanceLine>();
            line.CompanyId = openingBalance.CompanyId;
            line.ItemId = item.Id;
            line.ItemUnitId = item.ItemUnitId;
            line.StockOpeningBalance = openingBalance;
            line.CalculateAmounts();
            openingBalance.Lines.Add(line);
        }
    }

    private static void ReplaceLines(
        StockOpeningBalance openingBalance,
        IReadOnlyList<StockOpeningBalanceLineRequest> requests,
        IReadOnlyDictionary<int, ItemSnapshot> items,
        ApplicationDbContext dbContext)
    {
        var requestedItemIds = requests
            .Select(request => request.ItemId)
            .ToHashSet();
        var existingLinesByItemId = openingBalance.Lines
            .ToDictionary(line => line.ItemId);

        foreach (var line in openingBalance.Lines
                     .Where(line => !requestedItemIds.Contains(line.ItemId))
                     .ToList())
        {
            dbContext.StockOpeningBalanceLines.Remove(line);
            openingBalance.Lines.Remove(line);
        }

        foreach (var lineRequest in requests)
        {
            var item = items[lineRequest.ItemId];
            if (existingLinesByItemId.TryGetValue(
                    lineRequest.ItemId,
                    out var existingLine))
            {
                var normalizedLine = lineRequest.Adapt<StockOpeningBalanceLine>();
                existingLine.ItemUnitId = item.ItemUnitId;
                existingLine.Count = normalizedLine.Count;
                existingLine.Weight = normalizedLine.Weight;
                existingLine.Price = normalizedLine.Price;
                existingLine.Notes = normalizedLine.Notes;
                existingLine.CalculateAmounts();
                continue;
            }

            var line = lineRequest.Adapt<StockOpeningBalanceLine>();
            line.CompanyId = openingBalance.CompanyId;
            line.ItemId = item.Id;
            line.ItemUnitId = item.ItemUnitId;
            line.StockOpeningBalance = openingBalance;
            line.CalculateAmounts();
            openingBalance.Lines.Add(line);
        }
    }

    private async Task<Result<IReadOnlyDictionary<int, ItemSnapshot>>>
        ValidateRequestAsync(
            int storeId,
            IReadOnlyList<StockOpeningBalanceLineRequest> lines,
            CancellationToken cancellationToken)
    {
        var storeError = await ValidateStoreAsync(
            storeId,
            cancellationToken);
        if (storeError is not null)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(storeError);
        }

        return await ValidateLineItemsAsync(
            [.. lines.Select(line => line.ItemId)],
            cancellationToken);
    }

    private async Task<Error?> ValidateStoreAsync(
        int storeId,
        CancellationToken cancellationToken)
    {
        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == storeId)
            .Select(entity => new
            {
                entity.IsActive,
                entity.IsContainerStore
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (store is null)
        {
            return Error.NotFound(
                "StockOpeningBalances.StoreNotFound",
                $"لم يتم العثور على المخزن رقم {storeId}.",
                nameof(StockOpeningBalanceRequest.StoreId));
        }

        if (store.IsContainerStore)
        {
            return Error.Conflict(
                "StockOpeningBalances.ContainerStoreNotAllowed",
                "يجب اختيار مخزن منتجات وليس مخزن عبوات.",
                nameof(StockOpeningBalanceRequest.StoreId));
        }

        return store.IsActive
            ? null
            : Error.Conflict(
                "StockOpeningBalances.StoreInactive",
                "لا يمكن استخدام مخزن غير نشط.",
                nameof(StockOpeningBalanceRequest.StoreId));
    }

    private async Task<Result<IReadOnlyDictionary<int, ItemSnapshot>>>
        ValidateLineItemsAsync(
            IReadOnlyCollection<int> itemIds,
            CancellationToken cancellationToken)
    {
        var distinctItemIds = itemIds.Distinct().ToArray();
        var items = await dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                distinctItemIds.Contains(item.Id))
            .Select(item => new ItemSnapshot(
                item.Id,
                item.ItemUnitId,
                item.IsActive,
                item.ItemUnit.IsActive))
            .ToListAsync(cancellationToken);

        var itemsById = items.ToDictionary(item => item.Id);
        var missingIds = distinctItemIds
            .Where(itemId => !itemsById.ContainsKey(itemId))
            .ToArray();
        if (missingIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                Error.NotFound(
                    "StockOpeningBalances.ItemNotFound",
                    $"لم يتم العثور على الأصناف ذات الأرقام: {string.Join(", ", missingIds)}.",
                    nameof(StockOpeningBalanceLineRequest.ItemId)));
        }

        var inactiveItemIds = items
            .Where(item => !item.IsActive)
            .Select(item => item.Id)
            .ToArray();
        if (inactiveItemIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                Error.Conflict(
                    "StockOpeningBalances.ItemInactive",
                    $"لا يمكن استخدام الأصناف غير النشطة: {string.Join(", ", inactiveItemIds)}.",
                    nameof(StockOpeningBalanceLineRequest.ItemId)));
        }

        var inactiveUnitItemIds = items
            .Where(item => !item.ItemUnitIsActive)
            .Select(item => item.Id)
            .ToArray();
        return inactiveUnitItemIds.Length == 0
            ? Result<IReadOnlyDictionary<int, ItemSnapshot>>.Success(itemsById)
            : Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                Error.Conflict(
                    "StockOpeningBalances.ItemUnitInactive",
                    $"وحدات قياس الأصناف التالية غير نشطة: {string.Join(", ", inactiveUnitItemIds)}.",
                    nameof(StockOpeningBalanceLineRequest.ItemId)));
    }

    private static Error InvalidId() =>
        Error.Validation(
            "StockOpeningBalances.InvalidId",
            "يجب أن يكون رقم الرصيد الافتتاحي أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "StockOpeningBalances.NotFound",
            $"لم يتم العثور على الرصيد الافتتاحي رقم {id}.");

    private static Error DocumentNumberExists(string number) =>
        Error.Conflict(
            "StockOpeningBalances.DocumentNumberExists",
            $"رقم المستند '{number}' مستخدم بالفعل.",
            nameof(StockOpeningBalanceRequest.DocumentNumber));

    private static Error Concurrency() =>
        Error.Conflict(
            "StockOpeningBalances.Concurrency",
            "تم تعديل المستند بواسطة عملية أخرى؛ أعد تحميله ثم حاول مرة أخرى.");

    private sealed record ItemSnapshot(
        int Id,
        int ItemUnitId,
        bool IsActive,
        bool ItemUnitIsActive);
}
