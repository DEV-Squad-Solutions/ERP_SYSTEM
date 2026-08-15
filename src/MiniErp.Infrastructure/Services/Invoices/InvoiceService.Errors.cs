using MiniErp.Application.Common.Results;
using static MiniErp.Application.Features.Invoices.InvoiceErrors;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Invoices;

public sealed partial class InvoiceService
{
    private sealed record PreparedInvoice(
        CurrencyCode Currency,
        IReadOnlyDictionary<int, int> ItemUnitIds,
        IReadOnlyDictionary<int, PreparedReturnSourceLine> ReturnSourceLines,
        decimal? ReturnDiscountAmount);

    private sealed record PreparedReturnSourceLine(
        int SourceInvoiceLineId,
        int SourceInvoiceId,
        decimal UnitPrice);

    private sealed record PreparedReturnSources(
        IReadOnlyDictionary<int, PreparedReturnSourceLine> Lines,
        decimal? DiscountAmount)
    {
        public static PreparedReturnSources Empty { get; } =
            new(
                Lines: new Dictionary<int, PreparedReturnSourceLine>(),
                DiscountAmount: null);
    }
}
