using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

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

    Task<Result<OperationalTrialBalanceResponse>>
        GetOperationalTrialBalanceAsync(
            OperationalTrialBalanceFilterRequest filters,
            CancellationToken cancellationToken = default);

    Task<Result<TrialBalanceResponse>>
        GetTrialBalanceAsync(
            TrialBalanceFilterRequest filters,
            CancellationToken cancellationToken = default);

    Task<Result<FinancialStatementReportResponse>>
        GetFinancialStatementReportAsync(
            FinancialStatementType statementType,
            FinancialStatementReportRequest request,
            CancellationToken cancellationToken = default);

    Task<Result<EmployeeStatementResponse>> GetEmployeeStatementAsync(
        PaginationRequest pagination,
        EmployeeStatementFilterRequest filters,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAccountBalanceResponse>> GetEmployeeBalanceAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAccountSummaryResponse>> GetEmployeeAccountSummaryAsync(
        int employeeId,
        CancellationToken cancellationToken = default);
}
