using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Accounts;

public sealed record AccountResponse(
    int Id,
    int CompanyId,
    string Code,
    string Name,
    int? ParentAccountId,
    string? ParentAccountCode,
    string? ParentAccountName,
    AccountType AccountType,
    NormalBalance NormalBalance,
    bool IsPosting,
    bool IsActive,
    byte[] RowVersion);

public sealed record AccountSelectResponse(
    int Id,
    string Code,
    string Name,
    AccountType AccountType);

public sealed record ExpenseAccountSelectResponse(
    int Id,
    string Code,
    string Name,
    int? ParentAccountId,
    bool IsPosting);

public sealed record JournalAccountSelectResponse(
    int Id,
    string Code,
    string Name,
    AccountType AccountType,
    IReadOnlyList<JournalPartyGroupResponse> PartyGroups);

public sealed record JournalPartyGroupResponse(
    JournalPartyType PartyType,
    IReadOnlyList<JournalPartySelectResponse> Parties);

public sealed record JournalPartySelectResponse(
    int Id,
    string Code,
    string Name)
{
    public CurrencyCode? Currency { get; init; }
}

public sealed record AccountTreeResponse(
    int Id,
    string Code,
    string Name,
    int? ParentAccountId,
    AccountType AccountType,
    NormalBalance NormalBalance,
    bool IsPosting,
    bool IsActive,
    byte[] RowVersion,
    IReadOnlyList<AccountTreeResponse> Children);
