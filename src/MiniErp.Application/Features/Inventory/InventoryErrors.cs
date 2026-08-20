using MiniErp.Application.Common.Results;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.Inventory;

public static class InventoryErrors
{
    private const string CostFieldName = "unitCost";

    public static Error InsufficientStockAtDate(
        string itemName,
        int itemId,
        int storeId,
        DateOnly movementDate,
        decimal availableQuantity,
        decimal requestedQuantity,
        string fieldName) =>
        Error.Conflict(
            "Inventory.InsufficientStock",
            $"الكمية المتاحة للصنف {itemName} (رقم {itemId}) في المخزن " +
            $"{storeId} بتاريخ {movementDate:yyyy-MM-dd} هي " +
            $"{availableQuantity}، ولا يمكن صرف {requestedQuantity}.",
            fieldName);

    public static Error HistoricalStockConflict(
        string operationDescription,
        DateOnly movementDate,
        string itemName,
        int itemId,
        int storeId,
        DateOnly conflictDate,
        decimal availableQuantity,
        decimal requestedQuantity,
        string fieldName) =>
        Error.Conflict(
            "Inventory.HistoricalStockConflict",
            $"{operationDescription} بتاريخ {movementDate:yyyy-MM-dd} سيؤدي إلى " +
            $"عجز في رصيد الصنف {itemName} (رقم {itemId}) في المخزن {storeId} " +
            $"بتاريخ {conflictDate:yyyy-MM-dd}. الرصيد قبل حركة الصرف هو " +
            $"{availableQuantity}، وكمية الحركة هي {requestedQuantity}.",
            fieldName);

    public static Error InsufficientFinalStock(
        string itemName,
        int itemId,
        int storeId,
        decimal finalBalance,
        string fieldName) =>
        Error.Conflict(
            "Inventory.InsufficientStock",
            $"لا يوجد رصيد كافٍ للصنف {itemName} (رقم {itemId}) " +
            $"في المخزن {storeId} لتنفيذ الحركة. " +
            $"الرصيد النهائي المتوقع {finalBalance}.",
            fieldName);

    public static Error HistoricalFinalStockConflict(
        string itemName,
        int itemId,
        int storeId,
        string fieldName) =>
        Error.Conflict(
            "Inventory.HistoricalStockConflict",
            $"التعديل سيؤدي إلى رصيد نهائي سالب للصنف {itemName} " +
            $"(رقم {itemId}) في المخزن {storeId}.",
            fieldName);

    public static Error InvalidSalesReturnSource() =>
        Error.Validation(
            "Inventory.InvalidSalesReturnSource",
            "مرجع حركة البيع الأصلية غير صالح لاحتساب تكلفة مرتجع البيع.",
            "sourceInvoiceLineId");

    public static Error ReturnUnitCostRequired() =>
        Error.Validation(
            "Inventory.ReturnUnitCostRequired",
            "يجب إدخال تكلفة وحدة مرتجع البيع عند عدم توفر متوسط تكلفة موجب.",
            "returnUnitCost");

    public static Error TransferUnitCostRequired() =>
        Error.Conflict(
            "Inventory.TransferUnitCostRequired",
            "لا يوجد رصيد مُسعّر كافٍ في مخزن المصدر لإتمام التحويل. أضف رصيد الصنف وتكلفته أولاً أو قلّل كمية التحويل.",
            CostFieldName);

    public static Error InvalidInboundMovementType() =>
        Error.Validation(
            "Inventory.InvalidInboundMovementType",
            "نوع حركة المخزون الواردة غير صالح لاحتساب التكلفة.",
            "movementType");

    public static Error MissingSourceError(ItemMovement movement) =>
        Error.Validation(
            "Inventory.MovementCostSourceMissing",
            $"تعذر العثور على مصدر تكلفة حركة المخزون رقم {movement.Id}.",
            CostFieldName);
}
