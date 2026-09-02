using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.AccountStatementMappings;

public sealed record AccountStatementMappingResponse(
    int Id,
    int FiscalYearId,
    FinancialStatementType StatementType,
    int AccountId,
    string AccountCode,
    string AccountName,
    AccountType AccountType,
    int FinancialStatementLineId,
    string FinancialStatementLineCode,
    string FinancialStatementLineName);
