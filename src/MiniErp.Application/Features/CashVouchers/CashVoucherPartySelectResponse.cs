using MiniErp.Application.Common.Models;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherPartySelectResponse(
    IReadOnlyList<SelectResponse> BusinessPartners,
    IReadOnlyList<SelectResponse> Drivers,
    IReadOnlyList<SelectResponse> Employees);
