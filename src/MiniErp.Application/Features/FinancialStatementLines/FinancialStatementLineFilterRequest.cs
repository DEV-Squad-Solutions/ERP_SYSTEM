using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.FinancialStatementLines;

public sealed record FinancialStatementLineFilterRequest(
    int FiscalYearId,
    FinancialStatementType StatementType,
    string? Search = null,
    int? ParentLineId = null,
    bool? IsAssignable = null,
    bool? IsActive = null);
