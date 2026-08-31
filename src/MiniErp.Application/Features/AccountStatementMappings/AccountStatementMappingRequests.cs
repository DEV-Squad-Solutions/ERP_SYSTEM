namespace MiniErp.Application.Features.AccountStatementMappings;

public sealed record AccountStatementMappingRowRequest(
    int AccountId,
    int FinancialStatementLineId);

public sealed record ReplaceAccountStatementMappingsRequest(
    IReadOnlyList<AccountStatementMappingRowRequest> Mappings);
