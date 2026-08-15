using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Statements;

public interface IFinancialStatementService
{
    Task<Result<CashboxStatementResponse>> GetCashboxStatementAsync(
        PaginationRequest pagination,
        CashboxStatementFilterRequest filters,
        CancellationToken cancellationToken = default);

    Task<Result<PartnerStatementResponse>> GetPartnerStatementAsync(
        PaginationRequest pagination,
        PartnerStatementFilterRequest filters,
        CancellationToken cancellationToken = default);

    Task<Result<DriverStatementResponse>> GetDriverStatementAsync(
        PaginationRequest pagination,
        DriverStatementFilterRequest filters,
        CancellationToken cancellationToken = default);

    Task<Result<ContainerStoreStatementResponse>>
        GetContainerStoreStatementAsync(
            PaginationRequest pagination,
            ContainerStoreStatementFilterRequest filters,
            CancellationToken cancellationToken = default);
}
