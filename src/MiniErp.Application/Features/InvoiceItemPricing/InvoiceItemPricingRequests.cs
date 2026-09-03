using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.InvoiceItemPricing;

public sealed record InvoiceItemPricingFilterRequest(
    string? Search = null,
    int? InvoiceId = null,
    int? ItemId = null,
    InvoiceType? InvoiceType = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);

/// <summary>
/// Defines one advisory item expense. Amount is the expense for one item unit.
/// </summary>
public sealed record ItemPricingExpenseRequest(
    string Name,
    decimal Amount,
    string? Notes = null);

public sealed record ReplaceItemPricingExpensesRequest(
    IReadOnlyList<ItemPricingExpenseRequest>? Expenses);
