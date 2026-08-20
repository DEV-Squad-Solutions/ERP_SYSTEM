using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.ProfitabilityReports;

public interface IProfitabilityReportService
{
    Task<Result<InvoiceProfitabilityListResponse>> GetInvoicesAsync(
        PaginationRequest pagination,
        ProfitabilityReportFilterRequest filters,
        CancellationToken cancellationToken = default);

    Task<Result<InvoiceProfitabilityResponse>> GetInvoiceDetailsAsync(
        int invoiceId,
        CancellationToken cancellationToken = default);

    Task<Result<ItemProfitabilityListResponse>> GetItemsAsync(
        PaginationRequest pagination,
        ProfitabilityReportFilterRequest filters,
        CancellationToken cancellationToken = default);
}
