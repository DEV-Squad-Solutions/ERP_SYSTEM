using MiniErp.Application.Common.Results;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private sealed record PreparedInvoice(
        CurrencyCode Currency,
        IReadOnlyDictionary<int, int> ItemUnitIds);
}
