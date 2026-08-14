using MiniErp.Application.Features.BusinessPartners;
using MiniErp.Application.Features.Cashboxes;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.Containers;
using MiniErp.Application.Features.Countries;
using MiniErp.Application.Features.Drivers;
using MiniErp.Application.Features.InventoryCounts;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.Items;
using MiniErp.Application.Features.PartnerOpeningBalances;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Application.Features.StockOpeningBalances;
using MiniErp.Application.Features.StockTransfers;
using MiniErp.Application.Features.Stores;

namespace MiniErp.Tests.Common;

public sealed class ServerGeneratedIdentifierContractTests
{
    public static TheoryData<Type, string> ServerGeneratedIdentifiers => new()
    {
        { typeof(BusinessPartnerRequest), "Code" },
        { typeof(CashboxRequest), "Code" },
        { typeof(CashboxUpdateRequest), "Code" },
        { typeof(ContainerRequest), "Code" },
        { typeof(CountryRequest), "Code" },
        { typeof(DriverRequest), "Code" },
        { typeof(ItemRequest), "Code" },
        { typeof(StoreRequest), "Code" },
        { typeof(PartnerOpeningBalanceRequest), "DocumentNumber" },
        { typeof(PartnerOpeningBalanceUpdateRequest), "DocumentNumber" },
        { typeof(StockAdjustmentRequest), "DocumentNumber" },
        { typeof(StockAdjustmentUpdateRequest), "DocumentNumber" },
        { typeof(StockOpeningBalanceRequest), "DocumentNumber" },
        { typeof(StockOpeningBalanceUpdateRequest), "DocumentNumber" },
        { typeof(StockTransferRequest), "DocumentNumber" },
        { typeof(StockTransferUpdateRequest), "DocumentNumber" },
        { typeof(InventoryCountRequest), "DocumentNumber" },
        { typeof(InventoryCountUpdateRequest), "DocumentNumber" },
        { typeof(CashVoucherRequest), "VoucherNumber" },
        { typeof(CashVoucherUpdateRequest), "VoucherNumber" },
        { typeof(CashboxTransferRequest), "TransferNumber" },
        { typeof(CashboxTransferUpdateRequest), "TransferNumber" }
    };

    [Theory]
    [MemberData(nameof(ServerGeneratedIdentifiers))]
    public void RequestContract_DoesNotExposeServerGeneratedIdentifier(
        Type requestType,
        string identifierProperty)
    {
        Assert.DoesNotContain(
            requestType.GetProperties(),
            property => property.Name == identifierProperty);
    }

    [Fact]
    public void InvoiceRequest_KeepsClientEnteredInvoiceNumber()
    {
        Assert.Contains(
            typeof(InvoiceRequest).GetProperties(),
            property => property.Name == nameof(InvoiceRequest.InvoiceNumber));
    }
}
