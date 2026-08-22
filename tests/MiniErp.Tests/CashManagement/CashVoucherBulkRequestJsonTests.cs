using System.Text.Json;
using System.Text.Json.Serialization;
using MiniErp.Application.Features.CashVouchers;

namespace MiniErp.Tests.CashManagement;

public sealed class CashVoucherBulkRequestJsonTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    [Fact]
    public void Deserialize_UsesActionDiscriminatorForEveryItemShape()
    {
        const string json =
            """
            {
              "items": [
                {
                  "action": "Add",
                  "voucher": {
                    "voucherDate": "2026-08-21",
                    "direction": "Receipt",
                    "cashboxId": 1,
                    "amount": 100
                  }
                },
                {
                  "action": "Update",
                  "id": 7,
                  "rowVersion": "AAAAAAAAAAA=",
                  "voucher": {
                    "voucherDate": "2026-08-21",
                    "direction": "Payment",
                    "cashboxId": 1,
                    "cashMovementTypeId": null,
                    "amount": 25
                  }
                },
                {
                  "action": "Delete",
                  "id": 8,
                  "rowVersion": "AAAAAAAAAAA="
                }
              ]
            }
            """;

        var request = JsonSerializer.Deserialize<CashVoucherBulkRequest>(
            json,
            Options);

        Assert.NotNull(request?.Items);
        Assert.Collection(
            request.Items,
            item => Assert.Null(
                Assert.IsType<CashVoucherBulkAddItemRequest>(item)
                    .Voucher!.CashMovementTypeId),
            item => Assert.Null(
                Assert.IsType<CashVoucherBulkUpdateItemRequest>(item)
                    .Voucher!.CashMovementTypeId),
            item => Assert.IsType<CashVoucherBulkDeleteItemRequest>(item));
    }

    [Fact]
    public void Serialize_AddContainsActionAndVoucherWithoutIdentityFields()
    {
        CashVoucherBulkItemRequest item =
            new CashVoucherBulkAddItemRequest(Voucher: null);

        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(item, Options));
        var root = document.RootElement;

        Assert.Equal("Add", root.GetProperty("action").GetString());
        Assert.True(root.TryGetProperty("voucher", out _));
        Assert.False(root.TryGetProperty("id", out _));
        Assert.False(root.TryGetProperty("rowVersion", out _));
    }

    [Fact]
    public void Deserialize_AddRejectsIdentityFields()
    {
        const string json =
            """
            {
              "action": "Add",
              "id": 7,
              "rowVersion": "AAAAAAAAAAA=",
              "voucher": null
            }
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CashVoucherBulkItemRequest>(
                json,
                Options));
    }

    [Fact]
    public void Deserialize_DeleteRejectsVoucher()
    {
        const string json =
            """
            {
              "action": "Delete",
              "id": 7,
              "rowVersion": "AAAAAAAAAAA=",
              "voucher": null
            }
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CashVoucherBulkItemRequest>(
                json,
                Options));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(
                namingPolicy: null,
                allowIntegerValues: true));
        return options;
    }
}
