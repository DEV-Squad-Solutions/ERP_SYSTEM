using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private string GenerateInvoiceNumber()
    {
        var timestamp = timeProvider.GetUtcNow().UtcDateTime;
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"INV-{companyId}-{timestamp:yyyyMMddHHmmssfff}-{suffix}";
    }

    private static Error InvalidId() =>
        Error.Validation(
            "Invoices.InvalidId",
            "يجب أن يكون رقم الفاتورة أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "Invoices.NotFound",
            $"لم يتم العثور على الفاتورة رقم {id}.");

    private static Error Concurrency() =>
        Error.Conflict(
            "Invoices.Concurrency",
            "تم تعديل الفاتورة بواسطة مستخدم آخر. أعد تحميل الفاتورة ثم حاول مرة أخرى.");

    private sealed record PreparedInvoice(
        CurrencyCode Currency,
        IReadOnlyDictionary<int, int> ItemUnitIds);
}
