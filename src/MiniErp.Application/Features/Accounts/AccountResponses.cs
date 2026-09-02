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
