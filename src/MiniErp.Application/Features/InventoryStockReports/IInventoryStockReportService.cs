using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.InventoryStockReports;

public interface IInventoryStockReportService
{
    Task<Result<InventoryStockReportResponse>> GetAsync(
        PaginationRequest pagination,
        InventoryStockReportFilterRequest filters,
        CancellationToken cancellationToken = default);
}
