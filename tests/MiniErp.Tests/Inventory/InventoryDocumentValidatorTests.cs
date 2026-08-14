using MiniErp.Application.Features.InventoryCounts;
using MiniErp.Application.Features.StockAdjustments;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.Inventory;

public sealed class InventoryDocumentValidatorTests
{
    [Fact]
    public void StockAdjustmentValidator_RejectsDuplicateItemsAndBadPrecision()
    {
        var request = new StockAdjustmentRequest(
            1,
            new DateOnly(2026, 7, 28),
            StockAdjustmentDirection.Increase,
            null,
            [
                new StockAdjustmentLineRequest(1, 1.1234567m, null),
                new StockAdjustmentLineRequest(1, 2m, null)
            ]);

        var result = new StockAdjustmentRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(request.Lines));
        Assert.Contains(
            result.Errors,
            error => error.PropertyName.EndsWith(
                nameof(StockAdjustmentLineRequest.Quantity),
                StringComparison.Ordinal));
    }

    [Fact]
    public void InventoryCountUpdateValidator_AllowsZeroAndNullPhysicalCounts()
    {
        var request = new InventoryCountUpdateRequest(
            null,
            [
                new InventoryCountLineUpdateRequest(1, 0m, null),
                new InventoryCountLineUpdateRequest(2, null, null)
            ],
            new byte[8]);

        var result = new InventoryCountUpdateRequestValidator()
            .Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void InventoryCountUpdateValidator_RejectsNegativePhysicalCount()
    {
        var request = new InventoryCountUpdateRequest(
            null,
            [new InventoryCountLineUpdateRequest(1, -1m, null)],
            new byte[8]);

        var result = new InventoryCountUpdateRequestValidator()
            .Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName.EndsWith(
                nameof(InventoryCountLineUpdateRequest.PhysicalQuantity),
                StringComparison.Ordinal));
    }
}
