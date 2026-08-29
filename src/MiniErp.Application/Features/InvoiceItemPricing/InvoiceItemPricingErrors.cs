using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.InvoiceItemPricing;

public static class InvoiceItemPricingErrors
{
    public static Error InvalidFilters(string description, string fieldName) =>
        Error.Validation(
            "InvoiceItemPricing.InvalidFilters",
            description,
            fieldName);

    public static Error InvalidInvoiceLineId() =>
        Error.Validation(
            "InvoiceItemPricing.InvalidInvoiceLineId",
            "يجب تحديد سطر فاتورة صالح.",
            "InvoiceLineId");

    public static Error InvoiceLineNotFound(int invoiceLineId) =>
        Error.NotFound(
            "InvoiceItemPricing.InvoiceLineNotFound",
            $"سطر الفاتورة رقم {invoiceLineId} غير موجود أو لا يحتوي على صنف.",
            "InvoiceLineId");
}
