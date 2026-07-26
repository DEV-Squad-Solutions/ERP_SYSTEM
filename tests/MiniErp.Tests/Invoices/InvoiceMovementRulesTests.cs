using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.Invoices;

public sealed class InvoiceMovementRulesTests
{
    [Theory]
    [InlineData(InvoiceType.Sales, ItemMovementType.Sales, false)]
    [InlineData(InvoiceType.SalesReturn, ItemMovementType.SalesReturn, true)]
    [InlineData(InvoiceType.Purchase, ItemMovementType.Purchase, true)]
    [InlineData(InvoiceType.PurchaseReturn, ItemMovementType.PurchaseReturn, false)]
    public void ItemMovementDirectionMatchesInvoiceType(
        InvoiceType invoiceType,
        ItemMovementType movementType,
        bool inbound)
    {
        Assert.Equal(
            movementType,
            InvoiceMovementRules.GetItemMovementType(invoiceType));
        Assert.Equal(inbound, InvoiceMovementRules.IsInbound(invoiceType));
    }

    [Theory]
    [InlineData(InvoiceType.Sales, BusinessPartnerMovementType.Sales, 100, 0)]
    [InlineData(InvoiceType.SalesReturn, BusinessPartnerMovementType.SalesReturn, 0, 100)]
    [InlineData(InvoiceType.Purchase, BusinessPartnerMovementType.Purchase, 0, 100)]
    [InlineData(InvoiceType.PurchaseReturn, BusinessPartnerMovementType.PurchaseReturn, 100, 0)]
    public void PartnerMovementDirectionMatchesInvoiceType(
        InvoiceType invoiceType,
        BusinessPartnerMovementType movementType,
        decimal debit,
        decimal credit)
    {
        Assert.Equal(
            movementType,
            InvoiceMovementRules.GetPartnerMovementType(invoiceType));

        var amounts = InvoiceMovementRules.GetPartnerAmounts(
            invoiceType,
            100m);

        Assert.Equal(debit, amounts.Debit);
        Assert.Equal(credit, amounts.Credit);
    }

    [Theory]
    [InlineData(PaymentTerm.Cash, 100, false)]
    [InlineData(PaymentTerm.Credit, 100, true)]
    [InlineData(PaymentTerm.Credit, 0, false)]
    [InlineData(PaymentTerm.Credit, -1, false)]
    public void PartnerMovementIsCreatedOnlyForOutstandingCredit(
        PaymentTerm paymentTerm,
        decimal remainingAmount,
        bool expected)
    {
        Assert.Equal(
            expected,
            InvoiceMovementRules.ShouldCreatePartnerMovement(
                paymentTerm,
                remainingAmount));
    }
}
