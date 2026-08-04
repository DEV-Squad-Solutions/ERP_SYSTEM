using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Invoices;

public interface IInvoiceService
{
    Task<Result<InvoicePagedResponse>> GetAllAsync(
        PaginationRequest pagination,
        InvoiceFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<InvoiceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<InvoiceItemBalanceResponse>> GetItemBalanceAsync(
        int storeId,
        int itemId,
        DateOnly asOfDate,
        int? invoiceId = null,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<InvoiceReturnSourceResponse>>> GetReturnSourcesAsync(
        PaginationRequest pagination,
        InvoiceReturnSourceFilterRequest filters,
        CancellationToken cancellationToken = default);

    Task<Result<InvoiceResponse>> AddAsync(
        InvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<InvoiceResponse>> UpdateAsync(
        int id,
        InvoiceUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        byte[]? rowVersion,
        CancellationToken cancellationToken = default);
}
