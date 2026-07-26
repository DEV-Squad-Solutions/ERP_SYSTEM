using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Invoices;

public interface IInvoiceService
{
    Task<Result<PagedResponse<InvoiceListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<Result<InvoiceResponse>> GetByIdAsync(
        int id,
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
        CancellationToken cancellationToken = default);
}
