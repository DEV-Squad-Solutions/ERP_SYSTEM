using MiniErp.Application.Common.Models;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherPartySelectResponse(
    IReadOnlyList<SelectResponse> BusinessPartners,
    IReadOnlyList<SelectResponse> Drivers,
    IReadOnlyList<SelectResponse> Employees,
    IReadOnlyList<CashVoucherAccountSelectResponse> Expenses,
    IReadOnlyList<CashVoucherAccountSelectResponse> Revenues);

public sealed record CashVoucherAccountSelectResponse(
    int Id,
    string Name,
    CashMovementClassification Classification,
    string Code,
    AccountType AccountType);

// Kept as a schema-compatible descriptor for clients that consume the
// movement-type select model. Expense/revenue accounts are returned through
// CashVoucherAccountSelectResponse above.
public sealed record CashVoucherCashMovementSelectResponse(
    int Id,
    string Name,
    CashMovementClassification Classification);
