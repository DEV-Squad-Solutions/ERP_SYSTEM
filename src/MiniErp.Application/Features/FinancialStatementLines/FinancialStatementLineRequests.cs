using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.FinancialStatementLines;

public sealed record FinancialStatementLineRequest(
    int FiscalYearId,
    FinancialStatementType StatementType,
    string Code,
    string Name,
    int? ParentLineId,
    int DisplayOrder,
    bool IsAssignable,
    bool IsActive = true)
{
    public const int CodeMaximumLength = 50;

    public const int NameMaximumLength = 200;
}

public sealed record FinancialStatementLineUpdateRequest(
    int FiscalYearId,
    FinancialStatementType StatementType,
    string Code,
    string Name,
    int? ParentLineId,
    int DisplayOrder,
    bool IsAssignable,
    bool IsActive,
    byte[]? RowVersion);
