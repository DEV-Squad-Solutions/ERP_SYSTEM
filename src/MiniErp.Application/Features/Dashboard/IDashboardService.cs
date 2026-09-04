using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Dashboard;

public interface IDashboardService : IScopedService
{
    Task<Result<DashboardResponse>> GetAsync(
        DashboardFilterRequest filters,
        CancellationToken cancellationToken = default);
}
