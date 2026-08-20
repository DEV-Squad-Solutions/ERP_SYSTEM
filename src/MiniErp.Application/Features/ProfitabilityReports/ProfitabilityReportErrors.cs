using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.ProfitabilityReports;

public static class ProfitabilityReportErrors
{
    public static Error InvalidFilter(
        string fieldName,
        string description) =>
        Error.Validation(
            $"ProfitabilityReport.{fieldName}Invalid",
            description,
            fieldName);

    public static Error InvoiceNotFound(int invoiceId) =>
        Error.NotFound(
            "ProfitabilityReport.InvoiceNotFound",
            $"فاتورة البيع رقم {invoiceId} غير موجودة.",
            "invoiceId");
}
