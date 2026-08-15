using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.PartnerItemReports;

public interface IPartnerItemReportService
{
    Task<Result<PartnerItemReportResponse>> GetAsync(
        PartnerItemReportFilterRequest filters,
        CancellationToken cancellationToken = default);
}
