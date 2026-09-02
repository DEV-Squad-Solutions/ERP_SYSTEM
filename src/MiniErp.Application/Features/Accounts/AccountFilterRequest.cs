using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Accounts;

public sealed record AccountFilterRequest(
    string? Search = null,
    AccountType? AccountType = null,
    NormalBalance? NormalBalance = null,
    int? ParentAccountId = null,
    bool? IsPosting = null,
    bool? IsActive = null);
