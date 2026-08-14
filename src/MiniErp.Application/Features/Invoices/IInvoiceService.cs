using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Invoices;

public interface IInvoiceService
{
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
