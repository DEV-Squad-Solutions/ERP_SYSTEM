using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.FinancialStatementLines;

public sealed record FinancialStatementLineResponse(
    int Id,
    int CompanyId,
    int FiscalYearId,
    string FiscalYearName,
    FinancialStatementType StatementType,
    string Code,
    string Name,
    int? ParentLineId,
    string? ParentLineCode,
    string? ParentLineName,
    int DisplayOrder,
    bool IsAssignable,
    bool IsActive,
    byte[] RowVersion);

public sealed record FinancialStatementLineSelectResponse(
    int Id,
    string Code,
    string Name,
    int DisplayOrder);

public sealed record FinancialStatementLineTreeResponse(
    int Id,
    int FiscalYearId,
    FinancialStatementType StatementType,
    string Code,
    string Name,
    int? ParentLineId,
    int DisplayOrder,
    bool IsAssignable,
    bool IsActive,
    byte[] RowVersion,
    IReadOnlyList<FinancialStatementLineTreeResponse> Children);
