using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.StockAdjustments;

public sealed class StockAdjustmentService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IInventoryStockService inventoryStockService,
    TimeProvider timeProvider)
    : IStockAdjustmentService, IScopedService
{
    private static readonly ItemMovementType[] AdjustmentMovementTypes =
    [
        ItemMovementType.AdjustmentIncrease,
        ItemMovementType.AdjustmentDecrease
    ];

    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<StockAdjustmentListResponse>>>
        GetAllAsync(
            PaginationRequest pagination,
            StockAdjustmentFilterRequest? filters = null,
            CancellationToken cancellationToken = default)
    {
        filters ??= new StockAdjustmentFilterRequest();
        var filterError = ValidateFilters(filters);
        if (filterError is not null)
        {
            return Result<PagedResponse<StockAdjustmentListResponse>>.Failure(
                filterError);
        }

        var documentNumber = filters.DocumentNumber?.Trim();
        var query = dbContext.StockAdjustments
            .AsNoTracking()
            .Where(adjustment => adjustment.CompanyId == companyId)
            .Where(adjustment =>
                string.IsNullOrEmpty(documentNumber) ||
                adjustment.DocumentNumber.Contains(documentNumber))
            .Where(adjustment =>
                !filters.StoreId.HasValue ||
                adjustment.StoreId == filters.StoreId.Value)
            .Where(adjustment =>
                !filters.Direction.HasValue ||
                adjustment.Direction == filters.Direction.Value)
            .Where(adjustment =>
                !filters.FromDate.HasValue ||
                adjustment.DocumentDate >= filters.FromDate.Value)
            .Where(adjustment =>
                !filters.ToDate.HasValue ||
                adjustment.DocumentDate <= filters.ToDate.Value)
            .OrderByDescending(adjustment => adjustment.DocumentDate)
            .ThenByDescending(adjustment => adjustment.Id);

        return await paginationService.PaginateAsync<
            StockAdjustment,
            StockAdjustmentListResponse>(
                query,
                pagination,
                cancellationToken);
    }

    public async Task<Result<StockAdjustmentResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockAdjustmentResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<StockAdjustmentResponse>.Failure(NotFound(id))
            : Result<StockAdjustmentResponse>.Success(response);
    }

    public async Task<Result<StockAdjustmentResponse>> AddAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var requested = request.Adapt<StockAdjustment>();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var preparation = await ValidateRequestAsync(
            requested.StoreId,
            requested.Direction,
            request.Lines,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<StockAdjustmentResponse>.Failure(preparation.Error);
        }

        if (await DocumentNumberExistsAsync(
                requested.DocumentNumber,
                excludedId: null,
                cancellationToken))
        {
            return Result<StockAdjustmentResponse>.Failure(
                DocumentNumberExists(requested.DocumentNumber));
        }

        var stockError = await ValidateStockAsync(
            requested,
            request.Lines,
            replacedMovement: null,
            cancellationToken);
        if (stockError is not null)
        {
            return Result<StockAdjustmentResponse>.Failure(stockError);
        }

        requested.CompanyId = companyId;
        requested.SourceInventoryCountId = null;
        requested.Touch(timeProvider.GetUtcNow().UtcDateTime);
        AddLines(requested, request.Lines, preparation.Value);

        dbContext.StockAdjustments.Add(requested);
        await dbContext.SaveChangesAsync(cancellationToken);

        AddMovements(requested);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(requested.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<StockAdjustmentResponse>.Success(response);
    }

    public async Task<Result<StockAdjustmentResponse>> UpdateAsync(
        int id,
        StockAdjustmentUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<StockAdjustmentResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<StockAdjustmentResponse>.Failure(
                RowVersionRequired());
        }

        var requested = request.Adapt<StockAdjustment>();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var adjustment = await LoadForWriteAsync(id, cancellationToken);
        if (adjustment is null)
        {
            return Result<StockAdjustmentResponse>.Failure(NotFound(id));
        }

        if (adjustment.SourceInventoryCountId.HasValue)
        {
            return Result<StockAdjustmentResponse>.Failure(
                GeneratedAdjustmentImmutable());
        }

        if (!adjustment.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<StockAdjustmentResponse>.Failure(Concurrency());
        }

        var preparation = await ValidateRequestAsync(
            requested.StoreId,
            requested.Direction,
            request.Lines,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<StockAdjustmentResponse>.Failure(preparation.Error);
        }

        if (await DocumentNumberExistsAsync(
                requested.DocumentNumber,
                id,
                cancellationToken))
        {
            return Result<StockAdjustmentResponse>.Failure(
                DocumentNumberExists(requested.DocumentNumber));
        }

        var replacedMovement = MovementReference(
            adjustment.Id,
            adjustment.DocumentNumber);
        var stockError = await ValidateStockAsync(
            requested,
            request.Lines,
            replacedMovement,
            cancellationToken);
        if (stockError is not null)
        {
            return Result<StockAdjustmentResponse>.Failure(stockError);
        }

        var entry = dbContext.Entry(adjustment);
        entry.Property(item => item.RowVersion).OriginalValue =
            request.RowVersion;

        request.Adapt(adjustment);
        ReplaceLines(
            adjustment,
            request.Lines,
            preparation.Value);
        adjustment.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(item => item.LastModifiedAt).IsModified = true;

        var oldMovements = await LoadMovementsAsync(
            adjustment.Id,
            replacedMovement.ReferenceNumber,
            cancellationToken);
        dbContext.ItemMovements.RemoveRange(oldMovements);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            AddMovements(adjustment);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<StockAdjustmentResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<StockAdjustmentResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var adjustment = await LoadForWriteAsync(id, cancellationToken);
        if (adjustment is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (adjustment.SourceInventoryCountId.HasValue)
        {
            return Result.Failure(GeneratedAdjustmentImmutable());
        }

        if (StockAdjustmentMovementRules.IsInbound(adjustment.Direction))
        {
            var stockError = await inventoryStockService.ValidateTimelineAsync(
                new InventoryStockProposal(
                    adjustment.StoreId,
                    adjustment.DocumentDate,
                    IsInbound: true,
                    Lines: [],
                    MovementReference(
                        adjustment.Id,
                        adjustment.DocumentNumber),
                    $"حذف تسوية المخزون {adjustment.DocumentNumber}",
                    nameof(StockAdjustmentRequest.Lines)),
                cancellationToken);
            if (stockError is not null)
            {
                return Result.Failure(stockError);
            }
        }

        var movements = await LoadMovementsAsync(
            adjustment.Id,
            adjustment.DocumentNumber,
            cancellationToken);
        dbContext.ItemMovements.RemoveRange(movements);
        dbContext.StockAdjustmentLines.RemoveRange(adjustment.Lines);
        dbContext.StockAdjustments.Remove(adjustment);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<StockAdjustmentResponse> ProjectResponseQuery(int id) =>
        dbContext.StockAdjustments
            .Where(adjustment =>
                adjustment.CompanyId == companyId &&
                adjustment.Id == id)
            .ProjectToType<StockAdjustmentResponse>();

    private Task<StockAdjustment?> LoadForWriteAsync(
        int id,
        CancellationToken cancellationToken) =>
        dbContext.StockAdjustments
            .Include(adjustment => adjustment.Lines)
            .FirstOrDefaultAsync(
                adjustment =>
                    adjustment.CompanyId == companyId &&
                    adjustment.Id == id,
                cancellationToken);

    private async Task<Result<IReadOnlyDictionary<int, ItemSnapshot>>>
        ValidateRequestAsync(
            int storeId,
            StockAdjustmentDirection direction,
            IReadOnlyList<StockAdjustmentLineRequest> lines,
            CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(direction))
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                Error.Validation(
                    "StockAdjustments.DirectionInvalid",
                    "اتجاه تسوية المخزون غير مدعوم.",
                    nameof(StockAdjustmentRequest.Direction)));
        }

        if (lines.Count is < 1 or > StockAdjustmentRequest.MaximumLineCount ||
            lines.Any(line => line.ItemId <= 0 || line.Quantity <= 0m) ||
            lines.Select(line => line.ItemId).Distinct().Count() != lines.Count)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                Error.Validation(
                    "StockAdjustments.LinesInvalid",
                    "يجب إرسال سطور تسوية صحيحة بأصناف غير مكررة وكميات موجبة.",
                    nameof(StockAdjustmentRequest.Lines)));
        }

        var store = await dbContext.Stores
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == storeId)
            .Select(candidate => new
            {
                candidate.IsActive,
                candidate.IsContainerStore
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (store is null)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                StoreNotFound(storeId));
        }

        if (!store.IsActive)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                StoreInactive());
        }

        if (store.IsContainerStore)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ContainerStoreNotAllowed());
        }

        var itemIds = lines
            .Select(line => line.ItemId)
            .Distinct()
            .ToArray();
        var items = await dbContext.Items
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId &&
                itemIds.Contains(item.Id))
            .Select(item => new ItemSnapshot(
                item.Id,
                item.ItemUnitId,
                item.IsActive,
                item.ItemUnit.IsActive))
            .ToListAsync(cancellationToken);
        var itemsById = items.ToDictionary(item => item.Id);

        var missingItemIds = itemIds
            .Where(itemId => !itemsById.ContainsKey(itemId))
            .ToArray();
        if (missingItemIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ItemNotFound(missingItemIds));
        }

        var inactiveItemIds = items
            .Where(item => !item.IsActive)
            .Select(item => item.Id)
            .ToArray();
        if (inactiveItemIds.Length > 0)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ItemInactive(inactiveItemIds));
        }

        var inactiveUnitItemIds = items
            .Where(item => !item.ItemUnitIsActive)
            .Select(item => item.Id)
            .ToArray();
        return inactiveUnitItemIds.Length == 0
            ? Result<IReadOnlyDictionary<int, ItemSnapshot>>.Success(itemsById)
            : Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(
                ItemUnitInactive(inactiveUnitItemIds));
    }

    private Task<bool> DocumentNumberExistsAsync(
        string documentNumber,
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.StockAdjustments.AnyAsync(
            adjustment =>
                adjustment.CompanyId == companyId &&
                adjustment.DocumentNumber == documentNumber &&
                (!excludedId.HasValue ||
                 adjustment.Id != excludedId.Value),
            cancellationToken);

    private async Task<Error?> ValidateStockAsync(
        StockAdjustment adjustment,
        IReadOnlyCollection<StockAdjustmentLineRequest> lines,
        InventoryMovementReference? replacedMovement,
        CancellationToken cancellationToken) =>
        await inventoryStockService.ValidateTimelineAsync(
            new InventoryStockProposal(
                adjustment.StoreId,
                adjustment.DocumentDate,
                StockAdjustmentMovementRules.IsInbound(
                    adjustment.Direction),
                lines
                    .Select(line => new InventoryStockLine(
                        line.ItemId,
                        line.Quantity))
                    .ToArray(),
                replacedMovement,
                replacedMovement is null
                    ? $"إضافة تسوية المخزون {adjustment.DocumentNumber}"
                    : $"تعديل تسوية المخزون {adjustment.DocumentNumber}",
                nameof(StockAdjustmentRequest.Lines)),
            cancellationToken);

    private void AddLines(
        StockAdjustment adjustment,
        IReadOnlyCollection<StockAdjustmentLineRequest> requests,
        IReadOnlyDictionary<int, ItemSnapshot> items)
    {
        foreach (var request in requests)
        {
            var item = items[request.ItemId];
            var line = request.Adapt<StockAdjustmentLine>();
            line.CompanyId = companyId;
            line.ItemUnitId = item.ItemUnitId;
            adjustment.Lines.Add(line);
        }
    }

    private void ReplaceLines(
        StockAdjustment adjustment,
        IReadOnlyCollection<StockAdjustmentLineRequest> requests,
        IReadOnlyDictionary<int, ItemSnapshot> items)
    {
        var incomingByItem = requests.ToDictionary(line => line.ItemId);
        foreach (var existingLine in adjustment.Lines.ToList())
        {
            if (!incomingByItem.TryGetValue(
                    existingLine.ItemId,
                    out var incoming))
            {
                dbContext.StockAdjustmentLines.Remove(existingLine);
                adjustment.Lines.Remove(existingLine);
                continue;
            }

            existingLine.ItemUnitId = items[incoming.ItemId].ItemUnitId;
            existingLine.Quantity = incoming.Quantity;
            existingLine.Reason = string.IsNullOrWhiteSpace(incoming.Reason)
                ? null
                : incoming.Reason.Trim();
        }

        var existingItemIds = adjustment.Lines
            .Select(line => line.ItemId)
            .ToHashSet();
        foreach (var incoming in requests.Where(line =>
                     !existingItemIds.Contains(line.ItemId)))
        {
            var line = incoming.Adapt<StockAdjustmentLine>();
            line.CompanyId = companyId;
            line.ItemUnitId = items[incoming.ItemId].ItemUnitId;
            adjustment.Lines.Add(line);
        }
    }

    private void AddMovements(StockAdjustment adjustment)
    {
        var inbound = StockAdjustmentMovementRules.IsInbound(
            adjustment.Direction);
        var movementType = StockAdjustmentMovementRules.GetMovementType(
            adjustment.Direction);

        foreach (var line in adjustment.Lines.Where(line => !line.IsDeleted))
        {
            dbContext.ItemMovements.Add(
                new ItemMovement
                {
                    CompanyId = companyId,
                    StoreId = adjustment.StoreId,
                    ItemId = line.ItemId,
                    ItemUnitId = line.ItemUnitId,
                    MovementType = movementType,
                    ReferenceId = adjustment.Id,
                    ReferenceNumber = adjustment.DocumentNumber,
                    MovementDate = adjustment.DocumentDate,
                    QuantityIn = inbound ? line.Quantity : 0m,
                    QuantityOut = inbound ? 0m : line.Quantity,
                    Description =
                        $"Stock adjustment {adjustment.DocumentNumber}"
                });
        }
    }

    private Task<List<ItemMovement>> LoadMovementsAsync(
        int adjustmentId,
        string documentNumber,
        CancellationToken cancellationToken)
    {
        var movementTypes = AdjustmentMovementTypes;
        return dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movementTypes.Contains(movement.MovementType) &&
                movement.ReferenceId == adjustmentId &&
                movement.ReferenceNumber == documentNumber)
            .ToListAsync(cancellationToken);
    }

    private static InventoryMovementReference MovementReference(
        int adjustmentId,
        string documentNumber) =>
        new(
            AdjustmentMovementTypes,
            adjustmentId,
            documentNumber);

    private static Error? ValidateFilters(
        StockAdjustmentFilterRequest filters)
    {
        if (filters.StoreId is <= 0 ||
            filters.Direction.HasValue &&
            !Enum.IsDefined(filters.Direction.Value) ||
            filters.ToDate < filters.FromDate)
        {
            return Error.Validation(
                "StockAdjustments.FiltersInvalid",
                "مرشحات تسويات المخزون غير صحيحة.");
        }

        return null;
    }

    private sealed record ItemSnapshot(
        int Id,
        int ItemUnitId,
        bool IsActive,
        bool ItemUnitIsActive);

    private static Error InvalidId() =>
        Error.Validation(
            "StockAdjustments.InvalidId",
            "يجب أن يكون رقم تسوية المخزون أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "StockAdjustments.NotFound",
            $"لم يتم العثور على تسوية المخزون رقم {id}.");

    private static Error RowVersionRequired() =>
        Error.Validation(
            "StockAdjustments.RowVersionRequired",
            "يجب إرسال إصدار السجل الحالي المكون من 8 بايت للتعديل.",
            nameof(StockAdjustmentUpdateRequest.RowVersion));

    private static Error Concurrency() =>
        Error.Conflict(
            "StockAdjustments.Concurrency",
            "تم تعديل تسوية المخزون بواسطة مستخدم آخر. أعد تحميلها ثم حاول مرة أخرى.");

    private static Error DocumentNumberExists(string number) =>
        Error.Conflict(
            "StockAdjustments.DocumentNumberExists",
            $"رقم مستند التسوية '{number}' مستخدم بالفعل.",
            nameof(StockAdjustmentRequest.DocumentNumber));

    private static Error StoreNotFound(int id) =>
        Error.NotFound(
            "StockAdjustments.StoreNotFound",
            $"لم يتم العثور على المخزن رقم {id}.",
            nameof(StockAdjustmentRequest.StoreId));

    private static Error StoreInactive() =>
        Error.Conflict(
            "StockAdjustments.StoreInactive",
            "لا يمكن استخدام مخزن غير نشط.",
            nameof(StockAdjustmentRequest.StoreId));

    private static Error ContainerStoreNotAllowed() =>
        Error.Conflict(
            "StockAdjustments.ContainerStoreNotAllowed",
            "يجب اختيار مخزن منتجات وليس مخزن عبوات.",
            nameof(StockAdjustmentRequest.StoreId));

    private static Error ItemNotFound(IEnumerable<int> ids) =>
        Error.NotFound(
            "StockAdjustments.ItemNotFound",
            $"لم يتم العثور على الأصناف: {string.Join(", ", ids)}.",
            nameof(StockAdjustmentLineRequest.ItemId));

    private static Error ItemInactive(IEnumerable<int> ids) =>
        Error.Conflict(
            "StockAdjustments.ItemInactive",
            $"لا يمكن استخدام الأصناف غير النشطة: {string.Join(", ", ids)}.",
            nameof(StockAdjustmentLineRequest.ItemId));

    private static Error ItemUnitInactive(IEnumerable<int> ids) =>
        Error.Conflict(
            "StockAdjustments.ItemUnitInactive",
            $"وحدات قياس الأصناف التالية غير نشطة: {string.Join(", ", ids)}.",
            nameof(StockAdjustmentLineRequest.ItemId));

    private static Error GeneratedAdjustmentImmutable() =>
        Error.Conflict(
            "StockAdjustments.GeneratedAdjustmentImmutable",
            "تسوية المخزون المنشأة من مستند جرد غير قابلة للتعديل أو الحذف.");
}
