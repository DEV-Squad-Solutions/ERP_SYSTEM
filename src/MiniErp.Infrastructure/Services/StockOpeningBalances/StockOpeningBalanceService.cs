using System.Data;
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
        var requestError = ValidateRequestShape(request);
        if (requestError is not null)
        {
            return Result<StockOpeningBalanceResponse>.Failure(requestError);
        }

        var normalized = request.Adapt<StockOpeningBalance>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var validationResult = await ValidateRequestAsync(
            request,
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

        await transaction.CommitAsync(cancellationToken);
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

        var requestError = ValidateRequestShape(request);
        if (requestError is not null)
        {
            return Result<StockOpeningBalanceResponse>.Failure(requestError);
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

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
            request,
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
            return Result<StockOpeningBalanceResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var openingBalance = await LoadForWriteAsync(id, cancellationToken);
        if (openingBalance is null)
        {
            return Result.Failure(NotFound(id));
        }

        dbContext.StockOpeningBalanceLines.RemoveRange(openingBalance.Lines);
        dbContext.StockOpeningBalances.Remove(openingBalance);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
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
                     .Where(line => !requestedItemIds.Contains(line.ItemId)))
        {
            dbContext.StockOpeningBalanceLines.Remove(line);
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
            IStockOpeningBalanceRequest request,
            CancellationToken cancellationToken)
    {
        var storeError = await ValidateStoreAsync(
            request.StoreId,
            cancellationToken);
        if (storeError is not null)
        {
            return Result<IReadOnlyDictionary<int, ItemSnapshot>>.Failure(storeError);
        }

        return await ValidateLineItemsAsync(
            [.. request.Lines.Select(line => line.ItemId)],
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

    private static Error? ValidateRequestShape(IStockOpeningBalanceRequest request)
    {
        if (request is null)
        {
            return Error.Validation(
                "StockOpeningBalances.RequestRequired",
                "بيانات الرصيد الافتتاحي مطلوبة.");
        }

        if (request.StoreId <= 0)
        {
            return Error.Validation(
                "StockOpeningBalances.InvalidStoreId",
                "يجب أن يكون رقم المخزن أكبر من صفر.",
                nameof(StockOpeningBalanceRequest.StoreId));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentNumber))
        {
            return Error.Validation(
                "StockOpeningBalances.DocumentNumberRequired",
                "رقم المستند مطلوب.",
                nameof(StockOpeningBalanceRequest.DocumentNumber));
        }

        if (request.DocumentNumber.Length >
            StockOpeningBalanceRequest.DocumentNumberMaximumLength)
        {
            return Error.Validation(
                "StockOpeningBalances.DocumentNumberTooLong",
                $"لا يجوز أن يتجاوز رقم المستند {StockOpeningBalanceRequest.DocumentNumberMaximumLength} حرفاً.",
                nameof(StockOpeningBalanceRequest.DocumentNumber));
        }

        if (request.Notes?.Length >
            StockOpeningBalanceRequest.NotesMaximumLength)
        {
            return Error.Validation(
                "StockOpeningBalances.NotesTooLong",
                $"لا يجوز أن تتجاوز الملاحظات {StockOpeningBalanceRequest.NotesMaximumLength} حرف.",
                nameof(StockOpeningBalanceRequest.Notes));
        }

        if (request.DocumentDate == default)
        {
            return Error.Validation(
                "StockOpeningBalances.DocumentDateRequired",
                "تاريخ المستند مطلوب.",
                nameof(StockOpeningBalanceRequest.DocumentDate));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            return Error.Validation(
                "StockOpeningBalances.LinesRequired",
                "يجب إضافة سطر واحد على الأقل.",
                nameof(StockOpeningBalanceRequest.Lines));
        }

        if (request.Lines.Count > StockOpeningBalanceRequest.MaximumLineCount)
        {
            return Error.Validation(
                "StockOpeningBalances.TooManyLines",
                $"لا يجوز أن يتجاوز عدد السطور {StockOpeningBalanceRequest.MaximumLineCount}.",
                nameof(StockOpeningBalanceRequest.Lines));
        }

        if (request.Lines.Any(line => line is null))
        {
            return Error.Validation(
                "StockOpeningBalances.InvalidLine",
                "كل سطر في المستند مطلوب.",
                nameof(StockOpeningBalanceRequest.Lines));
        }

        if (request.Lines.Any(line => line.ItemId <= 0))
        {
            return Error.Validation(
                "StockOpeningBalances.InvalidItemId",
                "يجب أن يكون رقم الصنف أكبر من صفر.",
                nameof(StockOpeningBalanceLineRequest.ItemId));
        }

        if (request.Lines.Any(line => line.Count <= 0))
        {
            return Error.Validation(
                "StockOpeningBalances.InvalidCount",
                "يجب أن يكون عدد كل سطر أكبر من صفر.",
                nameof(StockOpeningBalanceLineRequest.Count));
        }

        if (request.Lines.Any(line =>
                line.Weight <= 0))
        {
            return Error.Validation(
                "StockOpeningBalances.InvalidWeight",
                "يجب أن يكون وزن كل سطر أكبر من صفر.",
                nameof(StockOpeningBalanceLineRequest.Weight));
        }

        if (request.Lines.Any(line => line.Price < 0))
        {
            return Error.Validation(
                "StockOpeningBalances.InvalidPrice",
                "يجب ألا يكون سعر السطر أقل من صفر.",
                nameof(StockOpeningBalanceLineRequest.Price));
        }

        if (request.Lines.Any(line =>
                !StockOpeningBalanceAmountRules.TryCalculate(
                    line.Count,
                    line.Weight,
                    line.Price,
                    out _,
                    out _)))
        {
            return Error.Validation(
                "StockOpeningBalances.InvalidCalculatedAmounts",
                "ناتج الكمية أو الإجمالي يتجاوز الدقة الرقمية المسموح بها.",
                nameof(StockOpeningBalanceRequest.Lines));
        }

        if (request.Lines.Any(line =>
                line.Notes?.Length >
                StockOpeningBalanceRequest.NotesMaximumLength))
        {
            return Error.Validation(
                "StockOpeningBalances.LineNotesTooLong",
                $"لا يجوز أن تتجاوز ملاحظات السطر {StockOpeningBalanceRequest.NotesMaximumLength} حرف.",
                nameof(StockOpeningBalanceLineRequest.Notes));
        }

        if (request.Lines.Select(line => line.ItemId).Distinct().Count() !=
            request.Lines.Count)
        {
            return Error.Validation(
                "StockOpeningBalances.DuplicateItemIds",
                "لا يجوز تكرار الصنف في سطور الرصيد الافتتاحي.",
                nameof(StockOpeningBalanceRequest.Lines));
        }

        return null;
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
