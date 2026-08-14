using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashMovementTypes;

public sealed record CashMovementTypeResponse(
    int Id,
    int CompanyId,
    string Name,
    CashDirection Direction,
    bool ForPartner,
    bool IsActive,
    bool IsDefaultForSales,
    bool IsDefaultForPurchase,
    bool IsDefaultForSalesReturn,
    bool IsDefaultForPurchaseReturn,
    string? Notes,
    byte[] RowVersion);

public sealed record CashMovementTypeSelectResponse(
    int Id,
    string Name,
    CashDirection Direction,
    bool IsDefaultForSales,
    bool IsDefaultForPurchase,
    bool IsDefaultForSalesReturn,
    bool IsDefaultForPurchaseReturn);
