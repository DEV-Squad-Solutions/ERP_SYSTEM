using MiniErp.Application.Common.Models;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherPartySelectResponse(
    IReadOnlyList<SelectResponse> BusinessPartners,
    IReadOnlyList<SelectResponse> Drivers,
    IReadOnlyList<SelectResponse> Employees,
    IReadOnlyList<CashVoucherCashMovementSelectResponse> Expenses,
    IReadOnlyList<CashVoucherCashMovementSelectResponse> Revenues);

public sealed record CashVoucherCashMovementSelectResponse(
    int Id,
    string Name,
    CashMovementClassification Classification);
