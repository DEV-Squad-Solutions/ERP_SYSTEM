using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public sealed record PartnerOpeningBalanceResponse(
    int Id,
    int CompanyId,
    int BusinessPartnerId,
    string BusinessPartnerName,
    string DocumentNumber,
    DateOnly DocumentDate,
    CurrencyCode Currency,
    PartnerBalanceType BalanceType,
    decimal Amount,
    string? Notes,
    byte[] RowVersion);
