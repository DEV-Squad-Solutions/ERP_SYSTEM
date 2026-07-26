using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Invoicing;

public static class InvoiceMovementRules
{
    public static ItemMovementType GetItemMovementType(
        InvoiceType invoiceType) =>
        invoiceType switch
        {
            InvoiceType.Sales => ItemMovementType.Sales,
            InvoiceType.SalesReturn => ItemMovementType.SalesReturn,
            InvoiceType.Purchase => ItemMovementType.Purchase,
            InvoiceType.PurchaseReturn => ItemMovementType.PurchaseReturn,
            _ => throw new ArgumentOutOfRangeException(nameof(invoiceType))
        };

    public static bool IsInbound(InvoiceType invoiceType) =>
        invoiceType is InvoiceType.Purchase or InvoiceType.SalesReturn;

    public static BusinessPartnerMovementType GetPartnerMovementType(
        InvoiceType invoiceType) =>
        invoiceType switch
        {
            InvoiceType.Sales => BusinessPartnerMovementType.Sales,
            InvoiceType.SalesReturn =>
                BusinessPartnerMovementType.SalesReturn,
            InvoiceType.Purchase => BusinessPartnerMovementType.Purchase,
            InvoiceType.PurchaseReturn =>
                BusinessPartnerMovementType.PurchaseReturn,
            _ => throw new ArgumentOutOfRangeException(nameof(invoiceType))
        };

    public static bool ShouldCreatePartnerMovement(
        PaymentTerm paymentTerm,
        decimal remainingAmount) =>
        paymentTerm == PaymentTerm.Credit && remainingAmount > 0m;

    public static (decimal Debit, decimal Credit) GetPartnerAmounts(
        InvoiceType invoiceType,
        decimal remainingAmount)
    {
        if (remainingAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingAmount));
        }

        return invoiceType is
            InvoiceType.Sales or InvoiceType.PurchaseReturn
            ? (remainingAmount, 0m)
            : (0m, remainingAmount);
    }
}
