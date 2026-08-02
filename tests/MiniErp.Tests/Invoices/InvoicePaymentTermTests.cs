using Mapster;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.Invoices;

public sealed class InvoicePaymentTermTests
{
    static InvoicePaymentTermTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public void CashInvoice_IsFullyPaidWhenPaidAmountEqualsTotal()
    {
        var invoice = CreateInvoice(PaymentTerm.Cash, 25m);

        Assert.Equal(1, (int)PaymentTerm.Cash);
        Assert.Equal(2, (int)PaymentTerm.Credit);
        Assert.Equal(PaymentTerm.Cash, invoice.PaymentTerm);
        Assert.Equal(PaymentStatus.Paid, invoice.GetPaymentStatus());
        Assert.Equal(25m, invoice.PaidAmount);
        Assert.Equal(0m, invoice.RemainingAmount);
    }

    [Fact]
    public void UnpaidPositiveInvoice_HasUnpaidStatus()
    {
        var invoice = CreateInvoice(PaymentTerm.Cash, 0m);

        Assert.Equal(PaymentStatus.Unpaid, invoice.GetPaymentStatus());
        Assert.Equal(0m, invoice.PaidAmount);
        Assert.Equal(25m, invoice.RemainingAmount);
    }

    [Fact]
    public void PartiallyPaidCreditInvoice_RemainsOutstandingAgainstPartner()
    {
        var invoice = CreateInvoice(PaymentTerm.Credit, 10m);

        Assert.Equal(PaymentStatus.PartiallyPaid, invoice.GetPaymentStatus());
        Assert.Equal(10m, invoice.PaidAmount);
        Assert.Equal(15m, invoice.RemainingAmount);
    }

    [Theory]
    [InlineData(PaymentTerm.Cash, 25, PaymentStatus.Paid, 0)]
    [InlineData(PaymentTerm.Cash, 0, PaymentStatus.Unpaid, 25)]
    [InlineData(PaymentTerm.Credit, 0, PaymentStatus.Unpaid, 25)]
    [InlineData(PaymentTerm.Credit, 10, PaymentStatus.PartiallyPaid, 15)]
    [InlineData(PaymentTerm.Credit, 25, PaymentStatus.Paid, 0)]
    public void Mapping_ExposesPaymentSummary(
        PaymentTerm paymentTerm,
        decimal paidAmount,
        PaymentStatus status,
        decimal remainingAmount)
    {
        var invoice = CreateInvoice(paymentTerm, paidAmount);
        invoice.BusinessPartner = new BusinessPartner { Name = "Partner" };
        invoice.Store = new Store { Name = "Store" };
        invoice.Lines.Single().Item = new Item
        {
            Code = "ITEM-1",
            Name = "Item",
            ItemUnit = new ItemUnit { Name = "Piece" }
        };

        var response = invoice.Adapt<InvoiceResponse>();

        Assert.Equal(paymentTerm, response.PaymentTerm);
        Assert.Equal(status, response.PaymentStatus);
        Assert.Equal(paidAmount, response.PaidAmount);
        Assert.Equal(25m, response.Subtotal);
        Assert.Equal(0m, response.DiscountAmount);
        Assert.Equal(remainingAmount, response.RemainingAmount);
    }

    [Theory]
    [InlineData(PaymentTerm.Cash, 25)]
    [InlineData(PaymentTerm.Credit, 0)]
    public void Validator_AcceptsBothPaymentTerms(
        PaymentTerm paymentTerm,
        decimal paidAmount)
    {
        var request = new InvoiceRequest(
            InvoiceNumber: "INV-TEST",
            InvoiceType: InvoiceType.Sales,
            ItemsCategoryId: null,
            ContentType: InvoiceContentType.Items,
            PaymentTerm: paymentTerm,
            InvoiceDate: new DateOnly(2026, 7, 25),
            DueDate: null,
            StoreId: 1,
            BusinessPartnerId: 1,
            PartnerInvoiceNo: null,
            CashboxId: paidAmount > 0m ? 1 : null,
            ExchangeRate: null,
            CashboxExchangeRate: null,
            WBWeight: 0m,
            WBScaleDifference: 0m,
            WBDiscount: 0m,
            ContainerStoreId: null,
            CountryId: null,
            DriverId: null,
            ActualDriverId: null,
            UsesExternalDriver: false,
            ExternalDriverName: null,
            VehicleNumber: null,
            ExportInvoiceCode: null,
            DiscountAmount: 0m,
            PaidAmount: paidAmount,
            Notes: null,
            Lines: [new InvoiceLineRequest(1, 1, 1m, 25m, null)],
            ContainerLines: []);

        var result = new InvoiceRequestValidator().Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Request_RequiresPaymentTermWithoutBackendDefault()
    {
        var constructor = Assert.Single(typeof(InvoiceRequest).GetConstructors());
        var paymentTerm = Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.Name == nameof(InvoiceRequest.PaymentTerm));

        Assert.Equal(typeof(PaymentTerm), paymentTerm.ParameterType);
        Assert.False(paymentTerm.IsOptional);
        Assert.False(paymentTerm.HasDefaultValue);
    }

    [Fact]
    public void RequestContracts_DoNotExposeServerCalculatedWBTotal()
    {
        Assert.DoesNotContain(
            typeof(InvoiceRequest).GetProperties(),
            property => property.Name == "WBTotal");
        Assert.DoesNotContain(
            typeof(InvoiceUpdateRequest).GetProperties(),
            property => property.Name == "WBTotal");
        Assert.Contains(
            typeof(InvoiceResponse).GetProperties(),
            property => property.Name == "WBTotal");
        Assert.Contains(
            typeof(InvoiceListResponse).GetProperties(),
            property => property.Name == "WBTotal");
        Assert.DoesNotContain(
            typeof(InvoiceRequest).GetProperties(),
            property => property.Name == "CashMovementTypeId");
        Assert.DoesNotContain(
            typeof(InvoiceUpdateRequest).GetProperties(),
            property => property.Name == "CashMovementTypeId");
        Assert.Contains(
            typeof(InvoiceResponse).GetProperties(),
            property => property.Name == "CashMovementTypeId");
    }

    [Fact]
    public void Validator_RejectsUnsupportedPaymentTerm()
    {
        var request = new InvoiceRequest(
            InvoiceNumber: "INV-TEST",
            InvoiceType: InvoiceType.Sales,
            ItemsCategoryId: null,
            ContentType: InvoiceContentType.Items,
            PaymentTerm: (PaymentTerm)999,
            InvoiceDate: new DateOnly(2026, 7, 25),
            DueDate: null,
            StoreId: 1,
            BusinessPartnerId: 1,
            PartnerInvoiceNo: null,
            CashboxId: null,
            ExchangeRate: null,
            CashboxExchangeRate: null,
            WBWeight: 0m,
            WBScaleDifference: 0m,
            WBDiscount: 0m,
            ContainerStoreId: null,
            CountryId: null,
            DriverId: null,
            ActualDriverId: null,
            UsesExternalDriver: false,
            ExternalDriverName: null,
            VehicleNumber: null,
            ExportInvoiceCode: null,
            DiscountAmount: 0m,
            PaidAmount: 0m,
            Notes: null,
            Lines: [new InvoiceLineRequest(1, 1, 1m, 25m, null)],
            ContainerLines: []);

        var result = new InvoiceRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(InvoiceRequest.PaymentTerm));
    }

    private static Invoice CreateInvoice(
        PaymentTerm paymentTerm,
        decimal paidAmount) =>
        CreateAndCalculate(new Invoice
        {
            PaymentTerm = paymentTerm,
            PaidAmount = paidAmount,
            Lines = [new InvoiceLine
            {
                Count = 1,
                Weight = 1m,
                Price = 25m
            }]
        });

    private static Invoice CreateAndCalculate(Invoice invoice)
    {
        invoice.CalculateTotal();
        return invoice;
    }
}
