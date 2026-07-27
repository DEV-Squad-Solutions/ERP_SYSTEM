using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Cashboxes;

public sealed record CashboxRequest(
    string Code,
    string Name,
    CurrencyCode Currency,
    decimal OpeningBalance,
    bool IsActive,
    string? Notes)
{
    public const int CodeMaximumLength = 50;

    public const int NameMaximumLength = 200;

    public const int NotesMaximumLength = 1_000;
}

public sealed record CashboxUpdateRequest(
    string Code,
    string Name,
    CurrencyCode Currency,
    decimal OpeningBalance,
    bool IsActive,
    string? Notes,
    byte[]? RowVersion);
