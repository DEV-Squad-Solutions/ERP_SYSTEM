using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.InventoryCostReports;

public interface IInventoryCostReportService
{
    Task<Result<InventoryCostReportResponse>> GetAsync(
        PaginationRequest pagination,
        InventoryCostReportFilterRequest filters,
        CancellationToken cancellationToken = default);
}
