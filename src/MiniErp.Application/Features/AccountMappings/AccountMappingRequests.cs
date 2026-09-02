using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.AccountMappings;

public sealed record AccountMappingRequest(
    AccountingMappingType MappingType,
    int? SourceId,
    int AccountId);

public sealed record ReplaceAccountMappingsRequest(
    IReadOnlyList<AccountMappingRequest> Mappings);
