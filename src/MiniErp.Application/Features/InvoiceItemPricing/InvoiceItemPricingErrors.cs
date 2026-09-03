using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.InvoiceItemPricing;

public static class InvoiceItemPricingErrors
{
    public static Error InvalidFilters(string description, string fieldName) =>
        Error.Validation(
            "InvoiceItemPricing.InvalidFilters",
            description,
            fieldName);

    public static Error InvalidItemId() =>
        Error.Validation(
            "InvoiceItemPricing.InvalidItemId",
            "يجب تحديد صنف صالح.",
            "ItemId");

    public static Error ItemNotFound(int itemId) =>
        Error.NotFound(
            "InvoiceItemPricing.ItemNotFound",
            $"الصنف رقم {itemId} غير موجود.",
            "ItemId");
}
