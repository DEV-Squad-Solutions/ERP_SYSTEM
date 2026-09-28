using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.Invoices;

public sealed class InvoiceLineTypeTests
{
    [Fact]
    public void ItemIdAutomaticallyClassifiesLineAsInventoryItem()
    {
        var line = new InvoiceLine
        {
            ItemId = 42
        };

        Assert.Equal(InvoiceLineType.InventoryItem, line.LineType);
    }

    [Fact]
    public void MissingItemIdAutomaticallyClassifiesLineAsService()
    {
        var line = new InvoiceLine();

        Assert.Equal(InvoiceLineType.Service, line.LineType);
    }
}
