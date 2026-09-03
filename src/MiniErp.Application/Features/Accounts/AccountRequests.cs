using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Accounts;

public sealed record AccountRequest(
    string? Code,
    string Name,
    int? ParentAccountId,
    AccountType AccountType,
    NormalBalance NormalBalance,
    bool IsPosting,
    bool IsActive = true)
{
    public const int CodeMaximumLength = 50;

    public const int NameMaximumLength = 200;
}

public sealed record AccountUpdateRequest(
    string Code,
    string Name,
    int? ParentAccountId,
    AccountType AccountType,
    NormalBalance NormalBalance,
    bool IsPosting,
    bool IsActive,
    byte[]? RowVersion);
