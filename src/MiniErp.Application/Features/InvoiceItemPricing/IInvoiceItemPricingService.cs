using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.InvoiceItemPricing;

public interface IInvoiceItemPricingService
{
    Task<Result<InvoiceItemPricingPagedResponse>> GetAsync(
        PaginationRequest pagination,
        InvoiceItemPricingFilterRequest filters,
        CancellationToken cancellationToken = default);

    Task<Result<ItemPricingExpensesResponse>> ReplaceExpensesAsync(
        int itemId,
        ReplaceItemPricingExpensesRequest request,
        CancellationToken cancellationToken = default);
}
