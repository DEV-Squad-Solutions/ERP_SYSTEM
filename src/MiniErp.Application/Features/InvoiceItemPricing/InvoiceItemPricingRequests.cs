using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.InvoiceItemPricing;

public sealed record InvoiceItemPricingFilterRequest(
    string? Search = null,
    int? InvoiceId = null,
    int? ItemId = null,
    InvoiceType? InvoiceType = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);

public sealed record InvoiceLinePricingExpenseRequest(
    string Name,
    decimal Amount,
    string? Notes = null);

public sealed record ReplaceInvoiceLinePricingExpensesRequest(
    IReadOnlyList<InvoiceLinePricingExpenseRequest>? Expenses);
