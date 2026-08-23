using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashMovementTypes;

public sealed record CashMovementTypeRequest(
    string Name,
    CashDirection Direction,
    CashMovementClassification? Classification,
    bool ForPartner,
    bool IsActive,
    bool IsDefaultForSales,
    bool IsDefaultForPurchase,
    bool IsDefaultForSalesReturn,
    bool IsDefaultForPurchaseReturn,
    string? Notes)
{
    public const int NameMaximumLength = 200;

    public const int NotesMaximumLength = 1_000;

    public CashMovementClassification ResolveClassification() =>
        Classification ??
        (ForPartner
            ? CashMovementClassification.PartnerSettlement
            : Direction == CashDirection.Receipt
                ? CashMovementClassification.Revenue
                : CashMovementClassification.Expense);
}

public sealed record CashMovementTypeUpdateRequest(
    string Name,
    CashDirection Direction,
    CashMovementClassification Classification,
    bool ForPartner,
    bool IsActive,
    bool IsDefaultForSales,
    bool IsDefaultForPurchase,
    bool IsDefaultForSalesReturn,
    bool IsDefaultForPurchaseReturn,
    string? Notes,
    byte[]? RowVersion);
