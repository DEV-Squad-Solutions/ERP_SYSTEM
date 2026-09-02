using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.AccountMappings;

public sealed record AccountMappingResponse(
    int Id,
    int FiscalYearId,
    string FiscalYearName,
    AccountingMappingType MappingType,
    int? SourceId,
    string? SourceCode,
    string? SourceName,
    int AccountId,
    string AccountCode,
    string AccountName,
    AccountType AccountType,
    byte[] RowVersion);
