using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Mappings;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Entities.ReferenceData;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Interceptors;
using MiniErp.Infrastructure.Services.Inventory;
using MiniErp.Infrastructure.Services.Invoices;
using MiniErp.Infrastructure.Services.Pagination;

namespace MiniErp.Tests.Invoices;

public sealed class InvoiceServiceTests
{
    static InvoiceServiceTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task Add_CreatesIndependentSalesReturnAndIncreasesStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn));

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceType.SalesReturn, result.Value.InvoiceType);

        var movement = await database.Context.ItemMovements.SingleAsync();
        Assert.Equal(ItemMovementType.SalesReturn, movement.MovementType);
        Assert.Equal(2m, movement.QuantityIn);
        Assert.Equal(0m, movement.QuantityOut);
    }

    [Fact]
    public async Task Add_NewInboundInvoiceSucceedsWithoutOpeningStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                storeId: 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.StoreId);
    }

    [Fact]
    public async Task Add_CreatesExactlyTheRequestedInvoiceLines()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                lines:
                [
                    new InvoiceLineRequest(1, 1, 1m, 10m, null),
                    new InvoiceLineRequest(2, 2, 1m, 20m, null)
                ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Lines.Count);
        Assert.Equal(2, await database.Context.InvoiceLines.CountAsync());
    }

    [Fact]
    public async Task Add_CreatesExactlyTheRequestedContainerLines()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                containerStoreId: 3,
                containerLines:
                [
                    new InvoiceContainerLineRequest(1, 1, 0)
                ]));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.ContainerLines);
        Assert.Equal(
            1,
            await database.Context.InvoiceContainerLines.CountAsync());
    }

    [Fact]
    public async Task Add_ContainerContentTypeCreatesContainerOnlyInvoice()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var request = CreateRequest(
            InvoiceType.Sales,
            storeId: 3,
            containerStoreId: 3,
            containerLines:
            [
                new InvoiceContainerLineRequest(1, 1, 0)
            ]) with
        {
            ContentType = InvoiceContentType.Containers,
            Lines = [],
            PaidAmount = 0m,
            CashboxId = null,
            CashMovementTypeId = null
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(InvoiceContentType.Containers, result.Value.ContentType);
        Assert.Empty(result.Value.Lines);
        Assert.Single(result.Value.ContainerLines);
        Assert.Empty(await database.Context.ItemMovements.ToListAsync());
        Assert.Single(await database.Context.ContainerMovements.ToListAsync());
    }

    [Fact]
    public async Task Add_UsesEnteredInvoiceNumberAndDoesNotDuplicateChildren()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                lines: [new InvoiceLineRequest(1, 2, 1m, 12m, null)],
                invoiceNumber: "  SALES-1001  "));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CompanyId);
        Assert.Equal("SALES-1001", result.Value.InvoiceNumber);
        Assert.Equal(24m, result.Value.Total);
        Assert.Single(result.Value.Lines);
        Assert.Equal(1, await database.Context.InvoiceLines.CountAsync());
    }

    [Fact]
    public async Task Add_AllowsDuplicateEnteredInvoiceNumber()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var first = await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                invoiceNumber: "INV-DUPLICATE"));

        var duplicate = await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                invoiceNumber: "  INV-DUPLICATE  "));

        Assert.True(first.IsSuccess);
        Assert.True(duplicate.IsSuccess);
        Assert.Equal("INV-DUPLICATE", duplicate.Value.InvoiceNumber);
        Assert.Equal(2, await database.Context.Invoices.CountAsync());
    }

    [Fact]
    public async Task Add_RejectsMissingEnteredInvoiceNumberInTheService()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(InvoiceType.SalesReturn) with
        {
            InvoiceNumber = "   "
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.InvoiceNumberInvalid", result.Error.Code);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
    }

    [Fact]
    public async Task Add_InboundInvoiceRejectsInvalidCalculatedQuantity()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                lines:
                [
                    new InvoiceLineRequest(
                        1,
                        1,
                        1_000_000_000_000m,
                        10m,
                        null)
                ]));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.InvalidCalculatedAmounts", result.Error.Code);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
        Assert.Equal(0, await database.Context.ItemMovements.CountAsync());
    }

    [Fact]
    public async Task Add_LineTotalOutsideMoneyPrecisionReturnsFailure()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                lines:
                [
                    new InvoiceLineRequest(
                        1,
                        2,
                        1m,
                        5_000_000_000_000_000m,
                        null)
                ]));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.InvalidCalculatedAmounts", result.Error.Code);
        Assert.Equal(
            0,
            await database.Context.Invoices
                .IgnoreQueryFilters()
                .CountAsync());
        Assert.Equal(
            0,
            await database.Context.InvoiceLines
                .IgnoreQueryFilters()
                .CountAsync());
        Assert.Equal(0, await database.Context.ItemMovements.CountAsync());
        Assert.Equal(
            0,
            await database.Context.BusinessPartnerMovements.CountAsync());
    }

    [Fact]
    public async Task Add_CreatesIndependentPurchaseReturnAndDecreasesStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.AddAsync(
            CreateRequest(InvoiceType.PurchaseReturn));

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceType.PurchaseReturn, result.Value.InvoiceType);

        var movement = await database.Context.ItemMovements.SingleAsync();
        Assert.Equal(ItemMovementType.PurchaseReturn, movement.MovementType);
        Assert.Equal(0m, movement.QuantityIn);
        Assert.Equal(2m, movement.QuantityOut);
    }

    [Fact]
    public async Task PurchaseReturn_UsesCurrentAverageNotEnteredPurchasePrice()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        await service.AddAsync(
            CreateRequest(
                InvoiceType.Purchase,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 10, 1m, 12m, null)]));

        var result = await service.AddAsync(
            CreateRequest(
                InvoiceType.PurchaseReturn,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 4, 1m, 99m, null)]));

        Assert.True(result.IsSuccess, result.Error.Description);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(12m, line.UnitCost);
        Assert.Equal(48m, line.InventoryTotalCost);
        Assert.Equal(6m, line.QuantityAfter);
        Assert.Equal(12m, line.AverageCostAfter);
        Assert.Equal(72m, line.InventoryValueAfter);
    }

    [Fact]
    public async Task LinkedSalesReturn_UsesFullyCostedSourceSaleCost()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        await service.AddAsync(
            CreateRequest(
                InvoiceType.Purchase,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 10, 1m, 12m, null)]));
        var sale = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 2, 1m, 30m, null)]))).Value;

        var result = await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                storeId: 2,
                lines:
                [
                    new InvoiceLineRequest(
                        1,
                        1,
                        1m,
                        30m,
                        null,
                        sale.Lines.Single().Id)
                ]));

        Assert.True(result.IsSuccess, result.Error.Description);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(sale.Lines.Single().Id, line.SourceInvoiceLineId);
        Assert.Equal(12m, line.UnitCost);
        Assert.Equal(12m, line.AverageCostAfter);
    }

    [Fact]
    public async Task Delete_BlocksSaleReferencedByActiveSalesReturn()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        await service.AddAsync(
            CreateRequest(
                InvoiceType.Purchase,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 10, 1m, 12m, null)]));
        var sale = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 2, 1m, 30m, null)]))).Value;
        await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                storeId: 2,
                lines:
                [
                    new InvoiceLineRequest(
                        1,
                        1,
                        1m,
                        30m,
                        null,
                        sale.Lines.Single().Id)
                ]));

        var result = await service.DeleteAsync(sale.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.LinkedSalesReturnsExist", result.Error.Code);
        Assert.Equal(
            3,
            await database.Context.Invoices.CountAsync());
    }

    [Fact]
    public async Task LinkedSalesReturn_RejectsPendingSourceSale()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            $"INSERT INTO CompanySettings (CompanyId, StockBalanceCheckMode) VALUES (1, {(int)StockBalanceCheckMode.None});");
        var service = database.CreateService();
        var sale = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 2, 1m, 30m, null)]))).Value;

        var result = await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                storeId: 2,
                lines:
                [
                    new InvoiceLineRequest(
                        1,
                        1,
                        1m,
                        30m,
                        null,
                        sale.Lines.Single().Id)
                ]));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Inventory.SalesReturnSourceCostPending",
            result.Error.Code);
        Assert.Equal(
            "لا يمكن احتساب تكلفة مرتجع البيع لأن حركة البيع الأصلية لم تكتمل تكلفتها بعد.",
            result.Error.Description);
        Assert.Equal(1, await database.Context.Invoices.CountAsync());
    }

    [Fact]
    public async Task UnlinkedSalesReturn_UsesPositiveAverageBeforeFallbackCost()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        await service.AddAsync(
            CreateRequest(
                InvoiceType.Purchase,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 10, 1m, 12m, null)]));

        var result = await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                storeId: 2,
                lines:
                [
                    new InvoiceLineRequest(
                        1,
                        1,
                        1m,
                        30m,
                        null,
                        null,
                        99m)
                ]));

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(12m, Assert.Single(result.Value.Lines).UnitCost);
    }

    [Fact]
    public async Task UnlinkedSalesReturn_RequiresCostWithoutPositiveAverage()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(
            InvoiceType.SalesReturn,
            storeId: 2) with
        {
            Lines = [new InvoiceLineRequest(1, 1, 1m, 10m, null)],
            PaidAmount = 10m
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.ReturnUnitCostRequired", result.Error.Code);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
    }

    [Fact]
    public async Task Add_BlocksPurchaseReturnWhenStockIsInsufficient()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var request = CreateRequest(
            InvoiceType.PurchaseReturn,
            lines: [new InvoiceLineRequest(1, 11, 1m, 10m, null)]);

        var result = await service.AddAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
        Assert.Contains("Item 1", result.Error.Description);
        Assert.Contains("(رقم 1)", result.Error.Description);
        Assert.Contains("في المخزن 1", result.Error.Description);
        Assert.Contains("2026-07-25", result.Error.Description);
        Assert.Contains("10", result.Error.Description);
        Assert.Contains("11", result.Error.Description);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
        Assert.Equal(0, await database.Context.ItemMovements.CountAsync());
    }

    [Fact]
    public async Task Add_ReportsMissingContainerStoreBeforeStockShortage()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                lines: [new InvoiceLineRequest(1, 11, 1m, 10m, null)],
                containerLines: [new InvoiceContainerLineRequest(1, 1, 0)]));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.ContainerStoreRequired", result.Error.Code);
    }

    [Fact]
    public async Task Add_ReportsForbiddenContainerLinesBeforeStockShortage()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.PurchaseReturn,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 1, 1m, 10m, null)],
                containerLines: [new InvoiceContainerLineRequest(1, 1, 0)]));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.ContainerLinesNotAllowed", result.Error.Code);
    }

    [Fact]
    public async Task Add_RejectsInvalidInvoiceTypeInTheService()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest((InvoiceType)99));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.InvoiceTypeInvalid", result.Error.Code);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
    }

    [Fact]
    public async Task Add_RejectsInvalidPaymentTermInTheService()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                paymentTerm: (PaymentTerm)99));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.PaymentTermInvalid", result.Error.Code);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
    }

    [Fact]
    public async Task Add_InternalDriverModeClearsExternalDriverName()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(InvoiceType.Sales) with
        {
            ExternalDriverName = "External driver"
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ExternalDriverName);
    }

    [Fact]
    public async Task Add_PurchaseReturnIncludesAdjustmentIncreaseInAvailableStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        database.Context.ItemMovements.Add(
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.AdjustmentIncrease,
                ReferenceId = 900,
                ReferenceNumber = "ADJ-900",
                MovementDate = new DateOnly(2026, 7, 24),
                QuantityIn = 5m,
                QuantityOut = 0m,
                Description = "Adjustment in"
            });
        await database.Context.SaveChangesAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO StockAdjustmentLines (
                CompanyId, StockAdjustmentId, ItemId, UnitCost, IsDeleted)
            VALUES (1, 900, 1, 0, 0);
            """);

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.PurchaseReturn,
                lines: [new InvoiceLineRequest(1, 12, 1m, 10m, null)]));

        Assert.True(result.IsSuccess, result.Error.Description);
    }

    [Fact]
    public async Task ValidateStockAsync_ShouldReject_WhenAffectedTimelineAlreadyContainsHistoricalShortage()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        database.Context.ItemMovements.AddRange(
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Sales,
                ReferenceId = 901,
                ReferenceNumber = "SALE-901",
                MovementDate = new DateOnly(2026, 1, 3),
                QuantityOut = 12m
            },
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Purchase,
                ReferenceId = 902,
                ReferenceNumber = "PURCHASE-902",
                MovementDate = new DateOnly(2026, 1, 4),
                QuantityIn = 10m
            });
        await database.Context.SaveChangesAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                invoiceDate: new DateOnly(2026, 1, 2),
                lines: [new InvoiceLineRequest(1, 1, 1m, 10m, null)]));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.HistoricalStockConflict", result.Error.Code);
        Assert.StartsWith(
            "إضافة الفاتورة بتاريخ 2026-01-02",
            result.Error.Description);
        Assert.Contains("Item 1", result.Error.Description);
        Assert.DoesNotContain("تعديل الفاتورة", result.Error.Description);
    }

    [Fact]
    public async Task ValidateStockAsync_FinalCheckUsesTheResultingBalanceOnly()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CompanySettings (CompanyId, StockBalanceCheckMode) VALUES (1, {(int)StockBalanceCheckMode.FinalCheck});");
        database.Context.ItemMovements.AddRange(
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Sales,
                ReferenceId = 911,
                ReferenceNumber = "SALE-911",
                MovementDate = new DateOnly(2026, 1, 3),
                QuantityOut = 12m
            },
            CostedMovement(new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Purchase,
                ReferenceId = 912,
                ReferenceNumber = "PURCHASE-912",
                MovementDate = new DateOnly(2026, 1, 4),
                QuantityIn = 10m
            }));
        await database.Context.SaveChangesAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                invoiceDate: new DateOnly(2026, 1, 2),
                lines: [new InvoiceLineRequest(1, 1, 1m, 10m, null)]));

        Assert.True(result.IsSuccess, result.Error.Description);
    }

    [Fact]
    public async Task Add_InboundInvoiceAllowsExistingHistoricalShortageBecauseItAddsStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        database.Context.ItemMovements.AddRange(
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Sales,
                ReferenceId = 904,
                ReferenceNumber = "SALE-904",
                MovementDate = new DateOnly(2026, 1, 3),
                QuantityOut = 12m
            },
            CostedMovement(new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Purchase,
                ReferenceId = 905,
                ReferenceNumber = "PURCHASE-905",
                MovementDate = new DateOnly(2026, 1, 4),
                QuantityIn = 10m
            }));
        await database.Context.SaveChangesAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                invoiceDate: new DateOnly(2026, 1, 5),
                lines: [new InvoiceLineRequest(1, 1, 1m, 10m, null)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, await database.Context.ItemMovements.CountAsync());
    }

    [Fact]
    public async Task Update_RejectsRemovingInboundLineThatWouldMakeLaterBalanceNegative()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        database.Context.ChangeTracker.Clear();
        database.Context.ItemMovements.Add(
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Sales,
                ReferenceId = 903,
                ReferenceNumber = "SALE-903",
                MovementDate = new DateOnly(2026, 7, 26),
                QuantityOut = 11m
            });
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(2, 1, 1m, 10m, null)]));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.HistoricalStockConflict", result.Error.Code);
        Assert.StartsWith(
            $"تعديل الفاتورة {created.InvoiceNumber} بتاريخ",
            result.Error.Description);
    }

    [Fact]
    public async Task Update_RejectsReducingInboundQuantityThatCausesLaterShortage()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Sales,
            referenceId: 904,
            referenceNumber: "SALE-904",
            movementDate: new DateOnly(2026, 7, 26),
            quantityOut: 12m);

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 1, 1m, 10m, null)]));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.HistoricalStockConflict", result.Error.Code);
    }

    [Fact]
    public async Task Update_MovesOutboundInvoiceToStoreWithSufficientStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        database.Context.ItemMovements.Add(
            CostedMovement(new ItemMovement
            {
                CompanyId = 1,
                StoreId = 2,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Purchase,
                ReferenceId = 910,
                ReferenceNumber = "PURCHASE-910",
                MovementDate = new DateOnly(2026, 7, 24),
                QuantityIn = 5m
            }));
        await database.Context.SaveChangesAsync();

        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.Sales))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(created, created.Lines
                .Select(line => new InvoiceLineRequest(
                    line.ItemId,
                    line.Count,
                    line.Weight,
                    line.Price,
                    line.Notes))
                .ToArray(), storeId: 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.StoreId);
        var movement = await database.Context.ItemMovements
            .Where(item => item.ReferenceId == created.Id)
            .SingleAsync();
        Assert.Equal(2, movement.StoreId);
    }

    [Fact]
    public async Task Update_BlocksOutboundInvoiceMoveToStoreWithInsufficientStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.Sales))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                storeId: 2));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
        Assert.Contains("2", result.Error.Description);
        var movement = await database.Context.ItemMovements
            .Where(item => item.ReferenceId == created.Id)
            .SingleAsync();
        Assert.Equal(1, movement.StoreId);
    }

    [Fact]
    public async Task Update_BlocksMovingInboundInvoiceWhenOldStoreWouldBecomeNegative()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        database.Context.ItemMovements.Add(
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Sales,
                ReferenceId = 911,
                ReferenceNumber = "SALE-911",
                MovementDate = new DateOnly(2026, 7, 26),
                QuantityOut = 11m
            });
        await database.Context.SaveChangesAsync();

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                storeId: 2));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.HistoricalStockConflict", result.Error.Code);
    }

    [Fact]
    public async Task Update_MovesInboundInvoiceWhenBothStoresRemainValid()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        database.Context.ItemMovements.Add(
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Sales,
                ReferenceId = 912,
                ReferenceNumber = "SALE-912",
                MovementDate = new DateOnly(2026, 7, 26),
                QuantityOut = 9m
            });
        await database.Context.SaveChangesAsync();

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                storeId: 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.StoreId);
    }

    [Fact]
    public async Task Update_DoesNotValidateUnrelatedStoreItemCombinations()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        database.Context.ItemMovements.Add(
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 2,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Sales,
                ReferenceId = 916,
                ReferenceNumber = "SALE-916",
                MovementDate = new DateOnly(2026, 7, 26),
                QuantityOut = 11m
            });
        await database.Context.SaveChangesAsync();

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(2, 2, 1m, 10m, null)],
                storeId: 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.StoreId);
        Assert.Equal(2, Assert.Single(result.Value.Lines).ItemId);
    }

    [Fact]
    public async Task Update_RejectsMissingCurrentInvoiceNumberExplicitly()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        database.Context.ChangeTracker.Clear();
        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Invoices SET InvoiceNumber = '' WHERE Id = {created.Id}");
        database.Context.ChangeTracker.Clear();

        var currentRowVersion = await database.Context.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.Id == created.Id)
            .Select(invoice => invoice.RowVersion)
            .SingleAsync();
        var requestInvoice = created with
        {
            InvoiceNumber = string.Empty,
            RowVersion = currentRowVersion
        };

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                requestInvoice,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)]));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Invoices.InvalidCurrentInvoiceReference",
            result.Error.Code);
    }

    [Theory]
    [InlineData(3, 3, true)]
    [InlineData(6, 5, false)]
    public async Task ValidateStock_AggregatesDuplicateItemQuantities(
        int firstCount,
        int secondCount,
        bool expectedSuccess)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var invoice = new Invoice
        {
            InvoiceType = InvoiceType.Sales,
            StoreId = 1,
            InvoiceDate = new DateOnly(2026, 7, 25)
        };
        var lines = new[]
        {
            new InvoiceLineRequest(1, firstCount, 1m, 10m, null),
            new InvoiceLineRequest(1, secondCount, 1m, 10m, null)
        };

        var error = await InvokeValidateStockAsync(
            database.CreateService(),
            invoice,
            lines,
            null,
            null);

        if (expectedSuccess)
        {
            Assert.Null(error);
        }
        else
        {
            Assert.NotNull(error);
            Assert.Equal("Inventory.InsufficientStock", error.Code);
        }
    }

    [Fact]
    public async Task ValidateStock_ExcludesOnlyMovementsMatchingBothInvoiceReferences()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Purchase,
            referenceId: 500,
            referenceNumber: "OTHER-NUMBER",
            movementDate: new DateOnly(2026, 7, 24),
            quantityIn: 5m);
        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Purchase,
            referenceId: 501,
            referenceNumber: "CURRENT",
            movementDate: new DateOnly(2026, 7, 24),
            quantityIn: 5m);
        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Sales,
            referenceId: 500,
            referenceNumber: "CURRENT",
            movementDate: new DateOnly(2026, 7, 24),
            quantityOut: 2m);
        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Purchase,
            referenceId: 502,
            referenceNumber: "OTHER",
            movementDate: new DateOnly(2026, 7, 24),
            quantityIn: 1m);

        var error = await InvokeValidateStockAsync(
            database.CreateService(),
            new Invoice
            {
                InvoiceType = InvoiceType.Sales,
                StoreId = 1,
                InvoiceDate = new DateOnly(2026, 7, 25)
            },
            [new InvoiceLineRequest(1, 20, 1m, 10m, null)],
            500,
            "CURRENT");

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateStock_RejectsCurrentInvoiceNumberWithoutId()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var method = typeof(InvoiceService).GetMethod(
            "ValidateStockAsync",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var invocation = method.Invoke(
            service,
            new object?[]
            {
                new Invoice(),
                Array.Empty<InvoiceLineRequest>(),
                null,
                "INV-1",
                CancellationToken.None
            });
        var task = Assert.IsType<Task<Error?>>(invocation);

        var error = await task;

        Assert.NotNull(error);
        Assert.Equal(
            "Invoices.InvalidCurrentInvoiceReference",
            error.Code);
    }

    [Fact]
    public async Task Update_WithoutChangingStoreStillWorks()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 3, 1m, 10m, null)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.StoreId);
        Assert.Equal(3m, Assert.Single(result.Value.Lines).Quantity);
    }

    [Fact]
    public async Task Update_ChangesWritableScalarProperties()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                storeId: 2,
                invoiceDate: new DateOnly(2026, 7, 26),
                paymentTerm: PaymentTerm.Credit));

        Assert.True(result.IsSuccess);
        Assert.Equal(created.Id, result.Value.Id);
        Assert.Equal(1, result.Value.CompanyId);
        Assert.Equal(2, result.Value.StoreId);
        Assert.Equal(new DateOnly(2026, 7, 26), result.Value.InvoiceDate);
        Assert.Equal(PaymentTerm.Credit, result.Value.PaymentTerm);
    }

    [Fact]
    public async Task Update_AddsUpdatesAndRemovesContainerLines()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                containerStoreId: 3,
                containerLines:
                [
                    new InvoiceContainerLineRequest(1, 1, 0)
                ]))).Value;

        var changed = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                containerLines:
                [
                    new InvoiceContainerLineRequest(1, 2, 1)
                ],
                containerStoreId: 3));

        Assert.True(changed.IsSuccess);
        var changedContainer = Assert.Single(changed.Value.ContainerLines);
        Assert.Equal(2, changedContainer.OutgoingUnits);
        Assert.Equal(1, changedContainer.IncomingUnits);

        database.Context.ChangeTracker.Clear();
        var removed = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                changed.Value,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                containerLines: [],
                containerStoreId: 3));

        Assert.True(
            removed.IsSuccess,
            removed.IsFailure ? removed.Error.Description : null);
        Assert.Empty(removed.Value.ContainerLines);
        Assert.Equal(
            0,
            await database.Context.InvoiceContainerLines.CountAsync());
    }

    [Fact]
    public async Task Update_ReplacesBusinessPartnerSideEffect()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                businessPartnerId: 2,
                paymentTerm: PaymentTerm.Credit));

        Assert.True(result.IsSuccess);
        var movement = await database.Context.BusinessPartnerMovements
            .SingleAsync();
        Assert.Equal(2, movement.BusinessPartnerId);
        Assert.Equal(created.Id, movement.InvoiceId);
    }

    [Fact]
    public async Task Update_ReplacesDriverTripSideEffect()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                driverId: 1))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                driverId: 2));

        Assert.True(result.IsSuccess);
        var trip = await database.Context.DriverTrips.SingleAsync();
        Assert.Equal(2, trip.DriverId);
        Assert.Equal(created.Id, trip.InvoiceId);
    }

    [Fact]
    public async Task Add_MainDriverOnlyCreatesTripWithNoActualDriver()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                driverId: 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.DriverId);
        Assert.Equal("Driver 1", result.Value.DriverName);
        Assert.Null(result.Value.ActualDriverId);
        Assert.Null(result.Value.ActualDriverName);

        var trip = await database.Context.DriverTrips
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(1, trip.DriverId);
        Assert.Null(trip.ActualDriverId);
    }

    [Fact]
    public async Task Add_MainAndActualDriversPersistsBothRoles()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(
            InvoiceType.SalesReturn,
            driverId: 1) with
        {
            ActualDriverId = 2
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.DriverId);
        Assert.Equal("Driver 1", result.Value.DriverName);
        Assert.Equal(2, result.Value.ActualDriverId);
        Assert.Equal("Driver 2", result.Value.ActualDriverName);

        var invoice = await database.Context.Invoices
            .AsNoTracking()
            .SingleAsync();
        var trip = await database.Context.DriverTrips
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(1, invoice.DriverId);
        Assert.Equal(2, invoice.ActualDriverId);
        Assert.Equal(1, trip.DriverId);
        Assert.Equal(2, trip.ActualDriverId);
    }

    [Fact]
    public async Task Add_RejectsActualDriverWithoutMainDriver()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(InvoiceType.SalesReturn) with
        {
            ActualDriverId = 2
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.MainDriverRequired", result.Error.Code);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
    }

    [Theory]
    [InlineData(999, "Invoices.ActualDriverNotFound")]
    [InlineData(3, "Invoices.ActualDriverInactive")]
    [InlineData(4, "Invoices.ActualDriverNotFound")]
    public async Task Add_RejectsInvalidActualDriver(
        int actualDriverId,
        string expectedError)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(
            InvoiceType.SalesReturn,
            driverId: 1) with
        {
            ActualDriverId = actualDriverId
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedError, result.Error.Code);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
    }

    [Fact]
    public async Task Add_SameMainAndActualDriverNormalizesActualDriverToNull()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(
            InvoiceType.SalesReturn,
            driverId: 1) with
        {
            ActualDriverId = 1
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.DriverId);
        Assert.Null(result.Value.ActualDriverId);
        var trip = await database.Context.DriverTrips
            .AsNoTracking()
            .SingleAsync();
        Assert.Null(trip.ActualDriverId);
    }

    [Fact]
    public async Task Add_RejectsInternalActualDriverWithExternalDriverMode()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(
            InvoiceType.SalesReturn,
            driverId: 1) with
        {
            ActualDriverId = 2,
            UsesExternalDriver = true,
            ExternalDriverName = "External Driver"
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Invoices.ExternalDriverWithActualDriver",
            result.Error.Code);
    }

    [Fact]
    public async Task Add_MainResponsibleDriverWithExternalPhysicalDriverSucceeds()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(
            InvoiceType.SalesReturn,
            driverId: 1) with
        {
            UsesExternalDriver = true,
            ExternalDriverName = "External Driver"
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.DriverId);
        Assert.Null(result.Value.ActualDriverId);
        Assert.True(result.Value.UsesExternalDriver);
        Assert.Equal("External Driver", result.Value.ExternalDriverName);

        var trip = await database.Context.DriverTrips
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(1, trip.DriverId);
        Assert.Null(trip.ActualDriverId);
    }

    [Fact]
    public async Task Update_ActualDriverReplacesAndClearsOneStableTrip()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                driverId: 1) with
            {
                ActualDriverId = 2
            })).Value;

        var changed = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)]) with
            {
                DriverId = 2,
                ActualDriverId = 1
            });

        Assert.True(changed.IsSuccess);
        Assert.Equal(1, await database.Context.DriverTrips.CountAsync());
        var changedTrip = await database.Context.DriverTrips
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(2, changedTrip.DriverId);
        Assert.Equal(1, changedTrip.ActualDriverId);

        database.Context.ChangeTracker.Clear();
        var cleared = await service.UpdateAsync(
            changed.Value.Id,
            CreateUpdateRequest(
                changed.Value,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)]) with
            {
                ActualDriverId = null
            });

        Assert.True(cleared.IsSuccess);
        Assert.Null(cleared.Value.ActualDriverId);
        Assert.Equal(1, await database.Context.DriverTrips.CountAsync());
        var clearedTrip = await database.Context.DriverTrips
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(2, clearedTrip.DriverId);
        Assert.Null(clearedTrip.ActualDriverId);
    }

    [Fact]
    public async Task Update_CreditToCashCreatesFullAndPaymentMovements()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                paymentTerm: PaymentTerm.Cash));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            2,
            await database.Context.BusinessPartnerMovements.CountAsync());
        Assert.Single(await database.Context.CashVouchers.ToListAsync());
    }

    [Fact]
    public async Task Update_CashToCreditCreatesPartnerMovement()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Cash))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                paymentTerm: PaymentTerm.Credit,
                paidAmount: 0m));

        Assert.True(result.IsSuccess);
        Assert.Single(await database.Context.BusinessPartnerMovements
            .ToListAsync());
    }

    [Fact]
    public async Task Delete_OutboundInvoiceRemovesAllInvoiceSideEffects()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                PaymentTerm.Credit,
                containerLines:
                [
                    new InvoiceContainerLineRequest(1, 1, 0)
                ],
                containerStoreId: 3,
                driverId: 1))).Value;

        var result = await service.DeleteAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
        Assert.Equal(0, await database.Context.InvoiceLines.CountAsync());
        Assert.Equal(
            0,
            await database.Context.InvoiceContainerLines.CountAsync());
        Assert.Equal(0, await database.Context.ItemMovements.CountAsync());
        Assert.Equal(0, await database.Context.ContainerMovements.CountAsync());
        Assert.Equal(
            0,
            await database.Context.BusinessPartnerMovements.CountAsync());
        Assert.Equal(0, await database.Context.DriverTrips.CountAsync());
        Assert.Equal(
            1,
            await database.Context.Invoices
                .IgnoreQueryFilters()
                .CountAsync());
    }

    [Fact]
    public async Task Delete_InboundInvoiceSucceedsWhenHistoryRemainsValid()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        var result = await service.DeleteAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
        Assert.Equal(0, await database.Context.ItemMovements.CountAsync());
    }

    [Fact]
    public async Task Delete_InboundInvoiceRejectsLaterShortageAndPreservesAllSideEffects()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                driverId: 1))).Value;

        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Sales,
            referenceId: 930,
            referenceNumber: "SALE-930",
            movementDate: new DateOnly(2026, 7, 26),
            quantityOut: 11m);

        var result = await service.DeleteAsync(created.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.HistoricalStockConflict", result.Error.Code);
        Assert.Equal(1, await database.Context.Invoices.CountAsync());
        Assert.Equal(1, await database.Context.InvoiceLines.CountAsync());
        Assert.Equal(2, await database.Context.ItemMovements.CountAsync());
        Assert.Equal(
            1,
            await database.Context.BusinessPartnerMovements.CountAsync());
        Assert.Equal(1, await database.Context.DriverTrips.CountAsync());
    }

    [Fact]
    public async Task Add_WhenSideEffectInsertFails_RollsBackInvoiceAndLines()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER AbortInvoiceItemMovementInsert
            BEFORE INSERT ON ItemMovements
            BEGIN
                SELECT RAISE(ABORT, 'forced invoice side-effect failure');
            END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.AddAsync(
                CreateRequest(
                    InvoiceType.Sales,
                    PaymentTerm.Credit,
                    containerLines:
                    [
                        new InvoiceContainerLineRequest(1, 1, 0)
                    ],
                    containerStoreId: 3,
                    driverId: 1,
                    discountAmount: 2m,
                    paidAmount: 3m)));

        database.Context.ChangeTracker.Clear();
        Assert.Equal(
            0,
            await database.Context.Invoices
                .IgnoreQueryFilters()
                .CountAsync());
        Assert.Equal(
            0,
            await database.Context.InvoiceLines
                .IgnoreQueryFilters()
                .CountAsync());
        Assert.Equal(
            0,
            await database.Context.InvoiceContainerLines
                .IgnoreQueryFilters()
                .CountAsync());
        Assert.Equal(
            0,
            await database.Context.ItemMovements
                .IgnoreQueryFilters()
                .CountAsync());
        Assert.Equal(
            0,
            await database.Context.ContainerMovements
                .IgnoreQueryFilters()
                .CountAsync());
        Assert.Equal(
            0,
            await database.Context.BusinessPartnerMovements
                .IgnoreQueryFilters()
                .CountAsync());
        Assert.Equal(
            0,
            await database.Context.DriverTrips
                .IgnoreQueryFilters()
                .CountAsync());
    }

    [Fact]
    public async Task Update_WhenSideEffectInsertFails_RollsBackAggregateAndOldSideEffects()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                containerLines:
                [
                    new InvoiceContainerLineRequest(1, 1, 0)
                ],
                containerStoreId: 3,
                driverId: 1,
                discountAmount: 1m,
                paidAmount: 2m))).Value;
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER AbortReplacementItemMovementUpdate
            BEFORE UPDATE ON ItemMovements
            BEGIN
                SELECT RAISE(ABORT, 'forced replacement side-effect failure');
            END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.UpdateAsync(
                created.Id,
                CreateUpdateRequest(
                    created,
                    [new InvoiceLineRequest(1, 4, 1m, 12m, null)],
                    invoiceDate: new DateOnly(2026, 7, 26),
                    containerLines:
                    [
                        new InvoiceContainerLineRequest(1, 0, 2)
                    ],
                    containerStoreId: 3,
                    driverId: 2,
                    discountAmount: 5m,
                    paidAmount: 7m)));

        database.Context.ChangeTracker.Clear();
        var invoice = await database.Context.Invoices
            .AsNoTracking()
            .Include(item => item.Lines)
            .Include(item => item.ContainerLines)
            .SingleAsync();
        var line = Assert.Single(invoice.Lines);
        var containerLine = Assert.Single(invoice.ContainerLines);
        var itemMovement = await database.Context.ItemMovements
            .AsNoTracking()
            .SingleAsync();
        var containerMovement = await database.Context.ContainerMovements
            .AsNoTracking()
            .SingleAsync();
        var partnerMovement = await database.Context.BusinessPartnerMovements
            .AsNoTracking()
            .SingleAsync(movement => movement.InvoiceId.HasValue);
        var driverTrip = await database.Context.DriverTrips
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(1, invoice.BusinessPartnerId);
        Assert.Equal(new DateOnly(2026, 7, 25), invoice.InvoiceDate);
        Assert.Equal(1m, invoice.DiscountAmount);
        Assert.Equal(2m, invoice.PaidAmount);
        Assert.Equal(19m, invoice.Total);
        Assert.Equal(2, line.Count);
        Assert.Equal(10m, line.Price);
        Assert.Equal(1, containerLine.OutgoingUnits);
        Assert.Equal(0, containerLine.IncomingUnits);
        Assert.Equal(2m, itemMovement.QuantityIn);
        Assert.Equal(0m, itemMovement.QuantityOut);
        Assert.Equal(1, containerMovement.OutgoingUnits);
        Assert.Equal(0, containerMovement.IncomingUnits);
        Assert.Equal(1, partnerMovement.BusinessPartnerId);
        Assert.Equal(19m, partnerMovement.Credit);
        Assert.Equal(1, driverTrip.DriverId);
    }

    [Fact]
    public async Task Delete_WhenInvoiceConcurrencyFails_RollsBackRemovedSideEffects()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                PaymentTerm.Credit,
                containerLines:
                [
                    new InvoiceContainerLineRequest(1, 1, 0)
                ],
                containerStoreId: 3,
                driverId: 1))).Value;
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER IgnoreInvoiceSoftDelete
            BEFORE UPDATE ON Invoices
            WHEN NEW.IsDeleted = 1
            BEGIN
                SELECT RAISE(IGNORE);
            END;
            """);

        var result = await service.DeleteAsync(created.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.Concurrency", result.Error.Code);
        Assert.Equal(1, await database.Context.Invoices.CountAsync());
        Assert.Equal(1, await database.Context.InvoiceLines.CountAsync());
        Assert.Equal(
            1,
            await database.Context.InvoiceContainerLines.CountAsync());
        Assert.Equal(1, await database.Context.ItemMovements.CountAsync());
        Assert.Equal(1, await database.Context.ContainerMovements.CountAsync());
        Assert.Equal(
            1,
            await database.Context.BusinessPartnerMovements.CountAsync());
        Assert.Equal(1, await database.Context.DriverTrips.CountAsync());
    }

    [Fact]
    public async Task Delete_WhenChildDeleteFails_RollsBackInvoiceAndSideEffects()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                PaymentTerm.Credit,
                containerLines:
                [
                    new InvoiceContainerLineRequest(1, 1, 0)
                ],
                containerStoreId: 3,
                driverId: 1))).Value;
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER AbortInvoiceLineSoftDelete
            BEFORE UPDATE ON InvoiceLines
            WHEN NEW.IsDeleted = 1
            BEGIN
                SELECT RAISE(ABORT, 'forced invoice line delete failure');
            END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.DeleteAsync(created.Id));

        database.Context.ChangeTracker.Clear();
        Assert.Equal(1, await database.Context.Invoices.CountAsync());
        Assert.Equal(1, await database.Context.InvoiceLines.CountAsync());
        Assert.Equal(
            1,
            await database.Context.InvoiceContainerLines.CountAsync());
        Assert.Equal(1, await database.Context.ItemMovements.CountAsync());
        Assert.Equal(1, await database.Context.ContainerMovements.CountAsync());
        Assert.Equal(
            1,
            await database.Context.BusinessPartnerMovements.CountAsync());
        Assert.Equal(1, await database.Context.DriverTrips.CountAsync());
    }

    [Fact]
    public async Task Update_AllowsAddingItemToOutboundInvoice()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.Sales))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [
                    new InvoiceLineRequest(1, 2, 1m, 10m, null),
                    new InvoiceLineRequest(2, 1, 1m, 10m, null)
                ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Lines.Count);
    }

    [Fact]
    public async Task Update_AllowsChangingInvoiceDateEarlierWhenHistoryRemainsValid()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        database.Context.ItemMovements.Add(
            CostedMovement(new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Sales,
                ReferenceId = 913,
                ReferenceNumber = "SALE-913",
                MovementDate = new DateOnly(2026, 7, 26),
                QuantityOut = 8m
            }));
        await database.Context.SaveChangesAsync();

        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                invoiceDate: new DateOnly(2026, 7, 25)))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                invoiceDate: new DateOnly(2026, 1, 2)));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 1, 2), result.Value.InvoiceDate);
    }

    [Fact]
    public async Task Update_AllowsChangingInvoiceDateLaterWhenHistoryRemainsValid()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        database.Context.ItemMovements.Add(
            new ItemMovement
            {
                CompanyId = 1,
                StoreId = 1,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Sales,
                ReferenceId = 914,
                ReferenceNumber = "SALE-914",
                MovementDate = new DateOnly(2026, 1, 3),
                QuantityOut = 8m
            });
        await database.Context.SaveChangesAsync();

        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                invoiceDate: new DateOnly(2026, 1, 2)))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                invoiceDate: new DateOnly(2026, 1, 4)));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 1, 4), result.Value.InvoiceDate);
    }

    [Theory]
    [InlineData(InvoiceType.SalesReturn, InvoiceType.Sales, 0, 2)]
    [InlineData(InvoiceType.Sales, InvoiceType.SalesReturn, 2, 0)]
    public async Task Update_ValidatesChangesBetweenInboundAndOutbound(
        InvoiceType originalType,
        InvoiceType updatedType,
        decimal expectedIn,
        decimal expectedOut)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(originalType))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                invoiceType: updatedType));

        Assert.True(result.IsSuccess);
        var movement = await database.Context.ItemMovements
            .Where(item => item.ReferenceId == created.Id)
            .SingleAsync();
        Assert.Equal(expectedIn, movement.QuantityIn);
        Assert.Equal(expectedOut, movement.QuantityOut);
    }

    [Fact]
    public async Task Add_ProcessesSameDateInboundBeforeOutbound()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        database.Context.ItemMovements.Add(
            CostedMovement(new ItemMovement
            {
                CompanyId = 1,
                StoreId = 2,
                ItemId = 1,
                ItemUnitId = 1,
                MovementType = ItemMovementType.Purchase,
                ReferenceId = 915,
                ReferenceNumber = "PURCHASE-915",
                MovementDate = new DateOnly(2026, 7, 25),
                QuantityIn = 2m
            }));
        await database.Context.SaveChangesAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 2, 1m, 10m, null)]));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Add_ProcessesOpeningBalanceBeforeSameDateOutboundMovement()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Sales,
            referenceId: 920,
            referenceNumber: "SALE-920",
            movementDate: new DateOnly(2026, 1, 1),
            quantityOut: 5m);

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                invoiceDate: new DateOnly(2026, 1, 1),
                lines: [new InvoiceLineRequest(1, 5, 1m, 10m, null)]));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Add_CombinesMultipleOpeningBalanceLinesForTheSameItem()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO StockOpeningBalances (
                Id, CompanyId, StoreId, DocumentDate, IsDeleted)
            VALUES (2, 1, 1, '2026-01-01', 0);

            INSERT INTO StockOpeningBalanceLines (
                Id, CompanyId, StockOpeningBalanceId, ItemId, Quantity,
                IsDeleted)
            VALUES (3, 1, 2, 1, 5, 0);
            """);

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                lines: [new InvoiceLineRequest(1, 15, 1m, 10m, null)]));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Update_ExcludesOldOutboundMovementWhenChangingQuantity()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.Sales))).Value;

        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Sales,
            referenceId: 921,
            referenceNumber: "SALE-921",
            movementDate: new DateOnly(2026, 7, 26),
            quantityOut: 7m);

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 3, 1m, 10m, null)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(3m, Assert.Single(result.Value.Lines).Quantity);
    }

    [Fact]
    public async Task Update_RejectsOutboundIncreaseWhenProposedQuantityExceedsStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await AddMovementAsync(
            database,
            storeId: 2,
            itemId: 1,
            movementType: ItemMovementType.Purchase,
            referenceId: 922,
            referenceNumber: "PURCHASE-922",
            movementDate: new DateOnly(2026, 7, 24),
            quantityIn: 5m);
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 2, 1m, 10m, null)]))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 6, 1m, 10m, null)]));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
        Assert.Contains("1", result.Error.Description);
        Assert.Contains("2", result.Error.Description);
        Assert.Contains("2026-07-25", result.Error.Description);
        Assert.Contains("5", result.Error.Description);
        Assert.Contains("6", result.Error.Description);
    }

    [Fact]
    public async Task Update_AllowsDecreasingOutboundQuantity()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                lines: [new InvoiceLineRequest(1, 4, 1m, 10m, null)]))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(2m, Assert.Single(result.Value.Lines).Quantity);
    }

    [Fact]
    public async Task Update_RejectsMovingOutboundInvoiceToAnEarlierDateWithInsufficientStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.Sales))).Value;

        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Sales,
            referenceId: 923,
            referenceNumber: "SALE-923",
            movementDate: new DateOnly(2026, 1, 2),
            quantityOut: 9m);

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                invoiceDate: new DateOnly(2026, 1, 2)));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
    }

    [Fact]
    public async Task Update_RejectsAddingOutboundItemWhenNewItemHasInsufficientStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.Sales))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [
                    new InvoiceLineRequest(1, 2, 1m, 10m, null),
                    new InvoiceLineRequest(2, 11, 1m, 10m, null)
                ]));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
    }

    [Fact]
    public async Task Update_RejectsChangingInboundInvoiceToOutboundWithoutStock()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                storeId: 2))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 1, 1m, 10m, null)],
                invoiceType: InvoiceType.Sales));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
    }

    [Fact]
    public async Task Add_DoesNotUseStockFromAnotherCompany()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Purchase,
            referenceId: 924,
            referenceNumber: "OTHER-COMPANY-924",
            movementDate: new DateOnly(2026, 7, 24),
            quantityIn: 100m,
            companyId: 2);

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                lines: [new InvoiceLineRequest(1, 11, 1m, 10m, null)]));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
    }

    [Fact]
    public async Task Add_DoesNotUseStockFromAnotherItem()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await AddMovementAsync(
            database,
            storeId: 2,
            itemId: 2,
            movementType: ItemMovementType.Purchase,
            referenceId: 925,
            referenceNumber: "PURCHASE-925",
            movementDate: new DateOnly(2026, 7, 24),
            quantityIn: 100m);

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                storeId: 2,
                lines: [new InvoiceLineRequest(1, 1, 1m, 10m, null)]));

        Assert.True(result.IsFailure);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);
    }

    [Theory]
    [InlineData(
        InvoiceType.SalesReturn,
        BusinessPartnerMovementType.SalesReturn,
        0,
        20)]
    [InlineData(
        InvoiceType.PurchaseReturn,
        BusinessPartnerMovementType.PurchaseReturn,
        20,
        0)]
    public async Task Add_CreditReturnCreatesCorrectPartnerMovement(
        InvoiceType invoiceType,
        BusinessPartnerMovementType movementType,
        decimal debit,
        decimal credit)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.AddAsync(
            CreateRequest(invoiceType, PaymentTerm.Credit));

        Assert.True(result.IsSuccess);
        var movement =
            await database.Context.BusinessPartnerMovements.SingleAsync();
        Assert.Equal(movementType, movement.MovementType);
        Assert.Equal(debit, movement.Debit);
        Assert.Equal(credit, movement.Credit);
    }

    [Theory]
    [InlineData(InvoiceType.SalesReturn)]
    [InlineData(InvoiceType.PurchaseReturn)]
    public async Task Add_CashReturnCreatesSettledPartnerMovements(
        InvoiceType invoiceType)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.AddAsync(CreateRequest(invoiceType));

        Assert.True(result.IsSuccess);
        var movements = await database.Context.BusinessPartnerMovements
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal(
            0m,
            movements.Sum(movement =>
                movement.Credit - movement.Debit));
    }

    [Theory]
    [InlineData(0, 0, 20, 20)]
    [InlineData(5, 3, 15, 12)]
    [InlineData(20, 0, 0, 0)]
    public void CalculateTotal_UsesDiscountAndPaidAmounts(
        decimal discountAmount,
        decimal paidAmount,
        decimal expectedTotal,
        decimal expectedRemaining)
    {
        var invoice = new Invoice
        {
            DiscountAmount = discountAmount,
            PaidAmount = paidAmount,
            Lines =
            [
                new InvoiceLine
                {
                    Count = 2,
                    Weight = 1m,
                    Price = 10m
                }
            ]
        };

        invoice.CalculateTotal();

        Assert.Equal(expectedTotal, invoice.Total);
        Assert.Equal(expectedRemaining, invoice.RemainingAmount);
    }

    [Fact]
    public async Task Add_CalculatesAndPersistsWeighbridgeTotal()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(
            InvoiceType.SalesReturn,
            PaymentTerm.Credit) with
        {
            WBWeight = 125.750000m,
            WBScaleDifference = 2.250000m,
            WBDiscount = 1.500000m
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal(125.750000m, result.Value.WBWeight);
        Assert.Equal(2.250000m, result.Value.WBScaleDifference);
        Assert.Equal(1.500000m, result.Value.WBDiscount);
        Assert.Equal(122.000000m, result.Value.WBTotal);

        database.Context.ChangeTracker.Clear();
        var persisted = await database.Context.Invoices.SingleAsync();
        Assert.Equal(122.000000m, persisted.WBTotal);
    }

    [Fact]
    public async Task Add_RejectsWeighbridgeDeductionsAboveWeight()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var request = CreateRequest(
            InvoiceType.SalesReturn,
            PaymentTerm.Credit) with
        {
            WBWeight = 10m,
            WBScaleDifference = 8m,
            WBDiscount = 3m
        };

        var result = await database.CreateService().AddAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.InvalidWBTotal", result.Error.Code);
        Assert.Empty(database.Context.Invoices);
    }

    [Theory]
    [InlineData(-1, 0, PaymentTerm.Credit, "Invoices.InvalidDiscountAmount")]
    [InlineData(21, 0, PaymentTerm.Credit, "Invoices.InvalidDiscountAmount")]
    [InlineData(0, -1, PaymentTerm.Credit, "Invoices.InvalidPaidAmount")]
    [InlineData(0, -1, PaymentTerm.Cash, "Invoices.InvalidPaidAmount")]
    [InlineData(0, 21, PaymentTerm.Credit, "Invoices.InvalidPaidAmount")]
    [InlineData(0, 21, PaymentTerm.Cash, "Invoices.InvalidPaidAmount")]
    public async Task Add_RejectsInvalidDiscountOrPaidAmount(
        decimal discountAmount,
        decimal paidAmount,
        PaymentTerm paymentTerm,
        string expectedCode)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                paymentTerm,
                discountAmount: discountAmount,
                paidAmount: paidAmount));

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(0, await database.Context.Invoices.CountAsync());
        Assert.Equal(
            0,
            await database.Context.BusinessPartnerMovements.CountAsync());
    }

    [Fact]
    public async Task Add_CashInvoicePersistsDiscountAndFullPayment()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();

        var result = await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Cash,
                discountAmount: 2m,
                paidAmount: 18m));

        Assert.True(result.IsSuccess);
        Assert.Equal(20m, result.Value.Subtotal);
        Assert.Equal(2m, result.Value.DiscountAmount);
        Assert.Equal(18m, result.Value.Total);
        Assert.Equal(18m, result.Value.PaidAmount);
        Assert.Equal(0m, result.Value.RemainingAmount);
        Assert.Equal(PaymentStatus.Paid, result.Value.PaymentStatus);
        Assert.Equal(
            2,
            await database.Context.BusinessPartnerMovements.CountAsync());
        var voucher = await database.Context.CashVouchers.SingleAsync();
        Assert.Equal(result.Value.Id, voucher.InvoiceId);
        Assert.Equal(voucher.Id, result.Value.PaymentVoucherId);
        Assert.Equal(voucher.CashboxId, result.Value.CashboxId);
        Assert.Equal(
            voucher.CashMovementTypeId,
            result.Value.CashMovementTypeId);
        Assert.Equal(18m, voucher.Amount);
        Assert.Equal(CashDirection.Payment, voucher.Direction);

        database.Context.ChangeTracker.Clear();
        var persisted = await database.Context.Invoices.SingleAsync();
        Assert.Equal(2m, persisted.DiscountAmount);
        Assert.Equal(18m, persisted.PaidAmount);
        Assert.Equal(18m, persisted.Total);
    }

    [Fact]
    public async Task Add_PersistsNormalizedPartnerInvoiceNumberInContracts()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var request = CreateRequest(
            InvoiceType.SalesReturn,
            PaymentTerm.Credit) with
        {
            PartnerInvoiceNo = "  PARTNER-INV-42  "
        };

        var created = await service.AddAsync(request);
        var details = await service.GetByIdAsync(created.Value.Id);
        var list = await service.GetAllAsync(
            new MiniErp.Application.Common.Models.PaginationRequest());

        Assert.True(created.IsSuccess);
        Assert.Equal("PARTNER-INV-42", created.Value.PartnerInvoiceNo);
        Assert.Equal("PARTNER-INV-42", details.Value.PartnerInvoiceNo);
        Assert.Equal(
            "PARTNER-INV-42",
            Assert.Single(list.Value.Items).PartnerInvoiceNo);

        database.Context.ChangeTracker.Clear();
        Assert.Equal(
            "PARTNER-INV-42",
            await database.Context.Invoices
                .Select(invoice => invoice.PartnerInvoiceNo)
                .SingleAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task Add_CashInvoiceRejectsOutstandingPaidAmount(
        decimal paidAmount)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Cash,
                paidAmount: paidAmount));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Invoices.CashInvoiceMustBeFullyPaid",
            result.Error.Code);
        Assert.Empty(await database.Context.Invoices.ToListAsync());
        Assert.Empty(await database.Context.CashVouchers.ToListAsync());
    }

    [Fact]
    public async Task Add_DiscountEqualToSubtotalCreatesZeroValueInvoice()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Cash,
                discountAmount: 20m,
                paidAmount: 0m));

        Assert.True(result.IsSuccess);
        Assert.Equal(20m, result.Value.Subtotal);
        Assert.Equal(0m, result.Value.Total);
        Assert.Equal(0m, result.Value.RemainingAmount);
        Assert.Equal(
            0,
            await database.Context.BusinessPartnerMovements.CountAsync());
    }

    [Theory]
    [InlineData(0, 20, 1)]
    [InlineData(7, 13, 2)]
    public async Task Add_CreditInvoiceUsesFullAndPaymentPartnerMovements(
        decimal paidAmount,
        decimal expectedRemaining,
        int expectedMovementCount)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                paidAmount: paidAmount));

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedRemaining, result.Value.RemainingAmount);
        var movements = await database.Context.BusinessPartnerMovements
            .ToListAsync();
        Assert.Equal(expectedMovementCount, movements.Count);
        var netCredit = movements.Sum(movement =>
            movement.Credit - movement.Debit);
        Assert.Equal(expectedRemaining, netCredit);
        var invoiceMovement = Assert.Single(
            movements,
            movement => movement.InvoiceId.HasValue);
        Assert.Equal(20m, invoiceMovement.Credit);
        Assert.Equal(
            paidAmount > 0m ? 1 : 0,
            await database.Context.CashVouchers.CountAsync());
    }

    [Fact]
    public async Task Add_FullyPaidCreditInvoiceIsRejected()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                paidAmount: 20m));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Invoices.CreditInvoiceCannotBeFullyPaid",
            result.Error.Code);
    }

    [Theory]
    [InlineData(InvoiceType.Sales, 18, 0)]
    [InlineData(InvoiceType.SalesReturn, 0, 18)]
    [InlineData(InvoiceType.Purchase, 0, 18)]
    [InlineData(InvoiceType.PurchaseReturn, 18, 0)]
    public async Task Add_PartiallySettledCreditInvoiceKeepsPartnerDirection(
        InvoiceType invoiceType,
        decimal expectedDebit,
        decimal expectedCredit)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().AddAsync(
            CreateRequest(
                invoiceType,
                PaymentTerm.Credit,
                discountAmount: 2m,
                paidAmount: 5m));

        Assert.True(
            result.IsSuccess,
            result.IsFailure ? result.Error.Description : null);
        Assert.Equal(13m, result.Value.RemainingAmount);
        Assert.Equal(PaymentStatus.PartiallyPaid, result.Value.PaymentStatus);
        var movement = await database.Context.BusinessPartnerMovements
            .SingleAsync(candidate => candidate.InvoiceId.HasValue);
        Assert.Equal(expectedDebit, movement.Debit);
        Assert.Equal(expectedCredit, movement.Credit);
        var paymentMovement = await database.Context.BusinessPartnerMovements
            .SingleAsync(candidate => candidate.CashVoucherId.HasValue);
        var voucher = await database.Context.CashVouchers.SingleAsync();
        var expectedPaymentDirection = invoiceType is
            InvoiceType.Sales or InvoiceType.PurchaseReturn
                ? CashDirection.Receipt
                : CashDirection.Payment;
        Assert.Equal(expectedPaymentDirection, voucher.Direction);
        Assert.Equal(result.Value.PaymentVoucherId, voucher.Id);
        Assert.Equal(
            expectedPaymentDirection == CashDirection.Payment ? 5m : 0m,
            paymentMovement.Debit);
        Assert.Equal(
            expectedPaymentDirection == CashDirection.Receipt ? 5m : 0m,
            paymentMovement.Credit);
        Assert.Equal(
            13m,
            Math.Abs(
                (movement.Credit - movement.Debit) +
                (paymentMovement.Credit - paymentMovement.Debit)));
    }

    [Theory]
    [InlineData(0, 0, 5, 0, 15)]
    [InlineData(2, 3, 5, 3, 12)]
    [InlineData(5, 3, 1, 3, 16)]
    public async Task Update_RecalculatesPartnerMovementFromDiscountAndPaidAmount(
        decimal originalDiscount,
        decimal originalPaid,
        decimal updatedDiscount,
        decimal updatedPaid,
        decimal expectedRemaining)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                discountAmount: originalDiscount,
                paidAmount: originalPaid))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                discountAmount: updatedDiscount,
                paidAmount: updatedPaid));

        Assert.True(result.IsSuccess);
        Assert.Equal(updatedDiscount, result.Value.DiscountAmount);
        Assert.Equal(updatedPaid, result.Value.PaidAmount);
        Assert.Equal(expectedRemaining, result.Value.RemainingAmount);
        var movements = await database.Context.BusinessPartnerMovements
            .ToListAsync();
        Assert.Equal(
            expectedRemaining,
            movements.Sum(movement =>
                movement.Credit - movement.Debit));
        var invoiceMovement = Assert.Single(
            movements,
            movement => movement.InvoiceId.HasValue);
        Assert.Equal(result.Value.Total, invoiceMovement.Credit);
    }

    [Fact]
    public async Task Update_PartialCreditToFullyPaidIsRejected()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                paidAmount: 5m))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                paidAmount: 20m));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Invoices.CreditInvoiceCannotBeFullyPaid",
            result.Error.Code);
        Assert.Equal(
            2,
            await database.Context.BusinessPartnerMovements.CountAsync());
    }

    [Fact]
    public async Task Update_PartialPaymentPreservesVoucherIdentityAndZeroRemovesIt()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                paidAmount: 5m))).Value;
        var originalVoucher = await database.Context.CashVouchers
            .AsNoTracking()
            .SingleAsync();

        var increased = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                paidAmount: 8m));

        Assert.True(increased.IsSuccess);
        database.Context.ChangeTracker.Clear();
        var updatedVoucher = await database.Context.CashVouchers
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(originalVoucher.Id, updatedVoucher.Id);
        Assert.Equal(originalVoucher.CreatedOn, updatedVoucher.CreatedOn);
        Assert.Equal(8m, updatedVoucher.Amount);
        Assert.Equal(updatedVoucher.Id, increased.Value.PaymentVoucherId);

        var removed = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                increased.Value,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                paidAmount: 0m));

        Assert.True(removed.IsSuccess);
        Assert.Equal(PaymentStatus.Unpaid, removed.Value.PaymentStatus);
        Assert.Null(removed.Value.PaymentVoucherId);
        Assert.Empty(await database.Context.CashVouchers.ToListAsync());
        Assert.True(
            await database.Context.CashVouchers
                .IgnoreQueryFilters()
                .Where(voucher => voucher.Id == originalVoucher.Id)
                .Select(voucher => voucher.IsDeleted)
                .SingleAsync());
    }

    [Fact]
    public async Task Update_UnpaidCreditToPartialCreatesPaymentVoucher()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                paidAmount: 0m))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                paidAmount: 8m));

        Assert.True(result.IsSuccess);
        Assert.Equal(12m, result.Value.RemainingAmount);
        var movements = await database.Context.BusinessPartnerMovements
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal(
            12m,
            movements.Sum(movement =>
                movement.Credit - movement.Debit));
        Assert.Single(await database.Context.CashVouchers.ToListAsync());
    }

    [Fact]
    public async Task Update_AmountsPreserveServerControlledInvoiceFields()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                paidAmount: 2m))).Value;
        var originalRowVersion = created.RowVersion;
        database.Context.ChangeTracker.Clear();
        var original = await database.Context.Invoices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync();

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                discountAmount: 4m,
                paidAmount: 6m));

        Assert.True(result.IsSuccess);
        Assert.Equal(created.Id, result.Value.Id);
        Assert.Equal(created.CompanyId, result.Value.CompanyId);
        Assert.Equal(created.InvoiceNumber, result.Value.InvoiceNumber);
        Assert.False(originalRowVersion.SequenceEqual(result.Value.RowVersion));

        database.Context.ChangeTracker.Clear();
        var persisted = await database.Context.Invoices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(original.CreatedById, persisted.CreatedById);
        Assert.Equal(original.CreatedOn, persisted.CreatedOn);
        Assert.Equal(4m, persisted.DiscountAmount);
        Assert.Equal(6m, persisted.PaidAmount);
    }

    [Fact]
    public async Task Update_CreditToFullyPaidCashPersistsPayment()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                paidAmount: 4m))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                paymentTerm: PaymentTerm.Cash,
                discountAmount: 3m,
                paidAmount: 17m));

        Assert.True(result.IsSuccess);
        Assert.Equal(17m, result.Value.Total);
        Assert.Equal(17m, result.Value.PaidAmount);
        Assert.Equal(0m, result.Value.RemainingAmount);
        Assert.Equal(
            2,
            await database.Context.BusinessPartnerMovements.CountAsync());
        Assert.Single(await database.Context.CashVouchers.ToListAsync());
    }

    [Fact]
    public async Task Update_ChangedCashTotalRequiresFullPayment()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Cash))).Value;
        var changedLines =
            new[] { new InvoiceLineRequest(1, 3, 1m, 10m, null) };

        var partial = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                changedLines,
                paidAmount: 20m));

        Assert.True(partial.IsFailure);
        Assert.Equal(
            "Invoices.CashInvoiceMustBeFullyPaid",
            partial.Error.Code);

        database.Context.ChangeTracker.Clear();
        var valid = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                changedLines,
                paidAmount: 30m));

        Assert.True(valid.IsSuccess);
        Assert.Equal(30m, valid.Value.Total);
        Assert.Equal(30m, valid.Value.PaidAmount);
        Assert.Equal(0m, valid.Value.RemainingAmount);
        Assert.Equal(
            2,
            await database.Context.BusinessPartnerMovements.CountAsync());
    }

    [Theory]
    [InlineData(-1, 0, PaymentTerm.Credit, "Invoices.InvalidDiscountAmount")]
    [InlineData(21, 0, PaymentTerm.Credit, "Invoices.InvalidDiscountAmount")]
    [InlineData(0, -1, PaymentTerm.Credit, "Invoices.InvalidPaidAmount")]
    [InlineData(0, 21, PaymentTerm.Credit, "Invoices.InvalidPaidAmount")]
    public async Task Update_RejectsInvalidDiscountOrPaidAmount(
        decimal discountAmount,
        decimal paidAmount,
        PaymentTerm paymentTerm,
        string expectedCode)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                paymentTerm: paymentTerm,
                discountAmount: discountAmount,
                paidAmount: paidAmount));

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(
            0m,
            await database.Context.Invoices
                .Select(invoice => invoice.DiscountAmount)
                .SingleAsync());
        Assert.Equal(
            0m,
            await database.Context.Invoices
                .Select(invoice => invoice.PaidAmount)
                .SingleAsync());
    }

    [Fact]
    public async Task Update_CashToPartialCreditCreatesRemainingMovement()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Cash))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
                paymentTerm: PaymentTerm.Credit,
                paidAmount: 6m));

        Assert.True(result.IsSuccess);
        Assert.Equal(14m, result.Value.RemainingAmount);
        var movements = await database.Context.BusinessPartnerMovements
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal(
            14m,
            movements.Sum(movement =>
                movement.Credit - movement.Debit));
    }

    [Fact]
    public async Task GetAll_FiltersBySpecificInvoiceType()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        await service.AddAsync(CreateRequest(InvoiceType.Purchase));
        await service.AddAsync(CreateRequest(InvoiceType.SalesReturn));

        var result = await service.GetAllAsync(
            new MiniErp.Application.Common.Models.PaginationRequest(),
            new InvoiceFilterRequest
            {
                InvoiceType = InvoiceType.Purchase
            });

        Assert.True(result.IsSuccess);
        var invoice = Assert.Single(result.Value.Items);
        Assert.Equal(InvoiceType.Purchase, invoice.InvoiceType);
    }

    [Fact]
    public async Task GetItemBalance_ReturnsBalanceThroughTheSelectedDate()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Purchase,
            referenceId: 940,
            referenceNumber: "PURCHASE-940",
            movementDate: new DateOnly(2026, 7, 24),
            quantityIn: 3m);
        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Sales,
            referenceId: 941,
            referenceNumber: "SALE-941",
            movementDate: new DateOnly(2026, 7, 25),
            quantityOut: 4m);
        await AddMovementAsync(
            database,
            storeId: 1,
            itemId: 1,
            movementType: ItemMovementType.Sales,
            referenceId: 942,
            referenceNumber: "SALE-942",
            movementDate: new DateOnly(2026, 7, 26),
            quantityOut: 5m);

        var result = await database.CreateService().GetItemBalanceAsync(
            storeId: 1,
            itemId: 1,
            asOfDate: new DateOnly(2026, 7, 25));

        Assert.True(result.IsSuccess);
        Assert.Equal("Product Store", result.Value.StoreName);
        Assert.Equal("Item 1", result.Value.ItemName);
        Assert.Equal("Unit", result.Value.ItemUnitName);
        Assert.Equal(9m, result.Value.Balance);
    }

    [Fact]
    public async Task GetItemBalance_CanExcludeTheInvoiceBeingEdited()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var invoice = (await service.AddAsync(
            CreateRequest(
                InvoiceType.Sales,
                lines: [new InvoiceLineRequest(1, 2, 1m, 10m, null)]))).Value;

        var currentBalance = await service.GetItemBalanceAsync(
            storeId: 1,
            itemId: 1,
            asOfDate: invoice.InvoiceDate);
        var availableForReplacement = await service.GetItemBalanceAsync(
            storeId: 1,
            itemId: 1,
            asOfDate: invoice.InvoiceDate,
            invoiceId: invoice.Id);

        Assert.True(currentBalance.IsSuccess);
        Assert.True(availableForReplacement.IsSuccess);
        Assert.Equal(8m, currentBalance.Value.Balance);
        Assert.Equal(10m, availableForReplacement.Value.Balance);
    }

    [Fact]
    public async Task GetItemBalance_DoesNotExposeAnotherCompanyStore()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Stores (
                Id, CompanyId, BusinessPartnerId, Code, Name, Address,
                IsContainerStore, IsActive, IsDeleted)
            VALUES (
                20, 2, NULL, 'OTHER-STORE', 'Other company store', NULL,
                0, 1, 0);
            """);
        database.Context.ChangeTracker.Clear();

        var result = await database.CreateService().GetItemBalanceAsync(
            storeId: 20,
            itemId: 1,
            asOfDate: new DateOnly(2026, 7, 25));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.StoreNotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetAll_SearchMatchesInvoiceDisplayValues()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await SeedInvoiceFilterDataAsync(database);

        var result = await database.CreateService().GetAllAsync(
            new MiniErp.Application.Common.Models.PaginationRequest(),
            new InvoiceFilterRequest
            {
                Search = "FILTER-PURCHASE"
            });

        Assert.True(result.IsSuccess);
        var invoice = Assert.Single(result.Value.Items);
        Assert.Equal("FILTER-PURCHASE-002", invoice.InvoiceNumber);
    }

    [Fact]
    public async Task GetAll_ReturnsSummaryForTheCompleteFilteredResult()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();

        await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                discountAmount: 2m,
                paidAmount: 5m));
        await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                discountAmount: 4m,
                paidAmount: 0m));
        await service.AddAsync(
            CreateRequest(
                InvoiceType.Purchase,
                PaymentTerm.Cash));

        var result = await service.GetAllAsync(
            new MiniErp.Application.Common.Models.PaginationRequest
            {
                PageSize = 1
            },
            new InvoiceFilterRequest
            {
                InvoiceType = InvoiceType.SalesReturn
            });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(40m, result.Value.Summary.Subtotal);
        Assert.Equal(6m, result.Value.Summary.DiscountAmount);
        Assert.Equal(34m, result.Value.Summary.Total);
        Assert.Equal(5m, result.Value.Summary.PaidAmount);
        Assert.Equal(29m, result.Value.Summary.RemainingAmount);
    }

    [Fact]
    public async Task GetAll_ReturnsZeroSummaryWhenNoInvoiceMatches()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        await service.AddAsync(CreateRequest(InvoiceType.Purchase));

        var result = await service.GetAllAsync(
            new MiniErp.Application.Common.Models.PaginationRequest(),
            new InvoiceFilterRequest
            {
                InvoiceNumber = "DOES-NOT-EXIST"
            });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.Equal(0m, result.Value.Summary.Subtotal);
        Assert.Equal(0m, result.Value.Summary.DiscountAmount);
        Assert.Equal(0m, result.Value.Summary.Total);
        Assert.Equal(0m, result.Value.Summary.PaidAmount);
        Assert.Equal(0m, result.Value.Summary.RemainingAmount);
    }

    [Fact]
    public async Task GetAll_RejectsUnsupportedInvoiceTypeFilter()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();

        var result = await database.CreateService().GetAllAsync(
            new MiniErp.Application.Common.Models.PaginationRequest(),
            new InvoiceFilterRequest
            {
                InvoiceType = (InvoiceType)999
            });

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.InvoiceTypeInvalid", result.Error.Code);
    }

    [Fact]
    public async Task GetAll_AppliesAllInvoiceFiltersTogether()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await SeedInvoiceFilterDataAsync(database);

        var result = await database.CreateService().GetAllAsync(
            new MiniErp.Application.Common.Models.PaginationRequest(),
            new InvoiceFilterRequest
            {
                InvoiceNumber = "  SALES-001  ",
                InvoiceType = InvoiceType.SalesReturn,
                BusinessPartnerId = 1,
                CountryId = 1,
                StoreId = 1,
                DriverId = 1,
                PaymentTerm = PaymentTerm.Cash,
                PriceStatus = InvoicePriceStatus.HasMissingPrice,
                FromDate = new DateOnly(2026, 7, 10),
                ToDate = new DateOnly(2026, 7, 10)
            });

        Assert.True(result.IsSuccess);
        var invoice = Assert.Single(result.Value.Items);
        Assert.Equal("FILTER-SALES-001", invoice.InvoiceNumber);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(1, result.Value.TotalPages);
    }

    [Theory]
    [InlineData(InvoicePriceStatus.HasMissingPrice, "FILTER-SALES-001")]
    [InlineData(InvoicePriceStatus.AllItemsPriced, "FILTER-PURCHASE-002")]
    public async Task GetAll_FiltersByLinePriceStatus(
        InvoicePriceStatus priceStatus,
        string expectedInvoiceNumber)
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await SeedInvoiceFilterDataAsync(database);

        var result = await database.CreateService().GetAllAsync(
            new MiniErp.Application.Common.Models.PaginationRequest(),
            new InvoiceFilterRequest
            {
                PriceStatus = priceStatus
            });

        Assert.True(result.IsSuccess);
        var invoice = Assert.Single(result.Value.Items);
        Assert.Equal(expectedInvoiceNumber, invoice.InvoiceNumber);
    }

    [Fact]
    public async Task GetAll_RejectsInvalidFiltersExplicitly()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var invalidFilters = new[]
        {
            new InvoiceFilterRequest
            {
                InvoiceNumber = new string(
                    'N',
                    InvoiceRequest.InvoiceNumberMaximumLength + 1)
            },
            new InvoiceFilterRequest
            {
                PaymentTerm = (PaymentTerm)999
            },
            new InvoiceFilterRequest
            {
                PriceStatus = (InvoicePriceStatus)999
            },
            new InvoiceFilterRequest
            {
                BusinessPartnerId = 0
            },
            new InvoiceFilterRequest
            {
                CountryId = -1
            },
            new InvoiceFilterRequest
            {
                StoreId = 0
            },
            new InvoiceFilterRequest
            {
                DriverId = -1
            },
            new InvoiceFilterRequest
            {
                FromDate = new DateOnly(2026, 7, 31),
                ToDate = new DateOnly(2026, 7, 1)
            }
        };

        foreach (var filters in invalidFilters)
        {
            var result = await service.GetAllAsync(
                new MiniErp.Application.Common.Models.PaginationRequest(),
                filters);

            Assert.True(result.IsFailure);
        }
    }

    [Fact]
    public async Task ResponsesExposePersistedDiscountPaidAndRemainingAmounts()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit,
                discountAmount: 3m,
                paidAmount: 4m))).Value;

        database.Context.ChangeTracker.Clear();
        var details = await service.GetByIdAsync(created.Id);
        var list = await service.GetAllAsync(
            new MiniErp.Application.Common.Models.PaginationRequest());

        Assert.True(details.IsSuccess);
        Assert.Equal(20m, details.Value.Subtotal);
        Assert.Equal(3m, details.Value.DiscountAmount);
        Assert.Equal(17m, details.Value.Total);
        Assert.Equal(4m, details.Value.PaidAmount);
        Assert.Equal(13m, details.Value.RemainingAmount);

        Assert.True(list.IsSuccess);
        var item = Assert.Single(list.Value.Items);
        Assert.Equal(20m, item.Subtotal);
        Assert.Equal(3m, item.DiscountAmount);
        Assert.Equal(17m, item.Total);
        Assert.Equal(4m, item.PaidAmount);
        Assert.Equal(13m, item.RemainingAmount);
        Assert.Equal(1, item.LineCount);
        Assert.Equal(0, item.ContainerLineCount);
    }

    [Fact]
    public async Task DatabaseDefaultsExistingInvoiceAmountsToZero()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Invoices (
                CompanyId, InvoiceNumber, InvoiceType, PaymentTerm,
                InvoiceDate, BusinessPartnerId, StoreId, Currency,
                UsesExternalDriver, Total, LastModifiedAt,
                CreatedById, CreatedOn, CreatedByPc, IsDeleted)
            VALUES (
                1, 'INV-LEGACY', 2, 2,
                '2026-07-25', 1, 1, 1,
                0, 20, '2026-07-25T00:00:00',
                '', '2026-07-25T00:00:00', '', 0);
            """);

        database.Context.ChangeTracker.Clear();
        var invoice = await database.Context.Invoices
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(0m, invoice.DiscountAmount);
        Assert.Equal(0m, invoice.PaidAmount);
    }

    [Fact]
    public async Task Update_ChangesAddsAndRemovesReturnLines()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        var changed = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 3, 1m, 5m, "Changed")]));

        Assert.True(changed.IsSuccess);
        var changedLine = Assert.Single(changed.Value.Lines);
        Assert.Equal(3m, changedLine.Quantity);
        Assert.Equal(15m, changedLine.Total);

        database.Context.ChangeTracker.Clear();
        var replaced = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                changed.Value,
                [new InvoiceLineRequest(2, 1, 4m, 2m, null)]));

        Assert.True(replaced.IsSuccess);
        var replacement = Assert.Single(replaced.Value.Lines);
        Assert.Equal(2, replacement.ItemId);
        Assert.Equal(4m, replacement.Quantity);
        Assert.Equal(8m, replaced.Value.Total);

        var persistedLines = await database.Context.InvoiceLines
            .IgnoreQueryFilters()
            .OrderBy(line => line.Id)
            .ToListAsync();
        Assert.Equal(2, persistedLines.Count);
        Assert.True(persistedLines[0].IsDeleted);
        Assert.False(persistedLines[1].IsDeleted);
    }

    [Fact]
    public async Task Add_RejectsDuplicateItemsBeforeDictionaryCreation()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var duplicate = new InvoiceLineRequest(1, 1, 1m, 10m, null);

        var result = await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                lines: [duplicate, duplicate]));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.DuplicateItemIds", result.Error.Code);
    }

    [Fact]
    public async Task Add_RejectsDuplicateContainersBeforeDictionaryCreation()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var duplicate = new InvoiceContainerLineRequest(1, 1, 0);

        var result = await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                containerLines: [duplicate, duplicate]));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.DuplicateContainerIds", result.Error.Code);
    }

    [Fact]
    public async Task Update_LineTotalOutsideMoneyPrecisionReturnsFailureAndPreservesData()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(
                InvoiceType.SalesReturn,
                PaymentTerm.Credit))).Value;

        var result = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [
                    new InvoiceLineRequest(
                        1,
                        2,
                        1m,
                        5_000_000_000_000_000m,
                        null)
                ]));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoices.InvalidCalculatedAmounts", result.Error.Code);

        database.Context.ChangeTracker.Clear();
        var invoice = await database.Context.Invoices
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .SingleAsync();
        var line = Assert.Single(invoice.Lines);
        var itemMovement =
            await database.Context.ItemMovements.AsNoTracking().SingleAsync();
        var partnerMovement = await database.Context.BusinessPartnerMovements
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(created.InvoiceNumber, invoice.InvoiceNumber);
        Assert.True(created.RowVersion.SequenceEqual(invoice.RowVersion));
        Assert.Equal(20m, invoice.Total);
        Assert.Equal(0m, invoice.DiscountAmount);
        Assert.Equal(0m, invoice.PaidAmount);
        Assert.Equal(2, line.Count);
        Assert.Equal(1m, line.Weight);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(10m, line.Price);
        Assert.Equal(20m, line.Total);
        Assert.Equal(2m, itemMovement.QuantityIn);
        Assert.Equal(0m, itemMovement.QuantityOut);
        Assert.Equal(0m, partnerMovement.Debit);
        Assert.Equal(20m, partnerMovement.Credit);
    }

    [Fact]
    public async Task Update_RejectsStaleRowVersion()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;

        var updated = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 12m, null)]));
        var stale = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 4, 1m, 12m, null)]));

        Assert.True(updated.IsSuccess);
        Assert.False(created.RowVersion.SequenceEqual(updated.Value.RowVersion));
        Assert.True(stale.IsFailure);
        Assert.Equal("Invoices.Concurrency", stale.Error.Code);
    }

    [Fact]
    public async Task Update_StaleRowVersionDoesNotReplaceTrackedOriginalValue()
    {
        await using var database = await InvoiceTestDatabase.CreateAsync();
        var service = database.CreateService();
        var created = (await service.AddAsync(
            CreateRequest(InvoiceType.SalesReturn))).Value;
        var updated = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 2, 1m, 12m, null)]));
        Assert.True(updated.IsSuccess);
        database.Context.ChangeTracker.Clear();

        var stale = await service.UpdateAsync(
            created.Id,
            CreateUpdateRequest(
                created,
                [new InvoiceLineRequest(1, 4, 1m, 12m, null)]));

        Assert.True(stale.IsFailure);
        Assert.Equal("Invoices.Concurrency", stale.Error.Code);
        var entry = Assert.Single(
            database.Context.ChangeTracker.Entries<Invoice>());
        var trackedOriginalValue =
            entry.Property(invoice => invoice.RowVersion).OriginalValue;
        Assert.True(
            updated.Value.RowVersion.SequenceEqual(entry.Entity.RowVersion));
        Assert.True(
            updated.Value.RowVersion.SequenceEqual(trackedOriginalValue));
        Assert.False(
            created.RowVersion.SequenceEqual(trackedOriginalValue));
    }

    [Fact]
    public void MapsterUpdate_DoesNotOverwriteProtectedInvoiceFields()
    {
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var line = new InvoiceLine
        {
            Count = 1,
            Weight = 2m,
            Price = 5m
        };
        var actualDriver = new Driver
        {
            Id = 2,
            CompanyId = 7,
            Code = "DRV-2",
            Name = "Actual Driver",
            LicenseNumber = "LIC-2"
        };
        var responsibleDriver = new Driver
        {
            Id = 1,
            CompanyId = 7,
            Code = "DRV-1",
            Name = "Responsible Driver",
            LicenseNumber = "LIC-1"
        };
        var businessPartner = new BusinessPartner
        {
            Id = 3,
            CompanyId = 7,
            Code = "BP-3",
            Name = "Partner"
        };
        var store = new Store
        {
            Id = 4,
            CompanyId = 7,
            Code = "STORE-4",
            Name = "Store"
        };
        var country = new Country
        {
            Id = 5,
            Code = "EG",
            Name = "Egypt",
            ArabicName = "مصر"
        };
        var invoice = new Invoice
        {
            Id = 41,
            CompanyId = 7,
            InvoiceNumber = "INV-SERVER",
            Currency = CurrencyCode.USD,
            CreatedById = "creator",
            BusinessPartner = businessPartner,
            Store = store,
            Country = country,
            Driver = responsibleDriver,
            ActualDriver = actualDriver,
            Lines = [line]
        };
        typeof(Invoice)
            .GetProperty(nameof(Invoice.RowVersion))!
            .SetValue(invoice, rowVersion);
        invoice.CalculateTotal();
        invoice.Touch(new DateTime(2026, 7, 25, 10, 30, 0, DateTimeKind.Utc));

        var request = new InvoiceUpdateRequest(
            InvoiceType.Purchase,
            PaymentTerm.Credit,
            new DateOnly(2026, 7, 26),
            null,
            99,
            88,
            null,
            null,
            1,
            2,
            false,
            "   ",
            "  VEH-2  ",
            "  EXT-2  ",
            3m,
            4m,
            "  Changed  ",
            [new InvoiceLineRequest(2, 5, 5m, 5m, null)],
            [],
            new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 });

        request.Adapt(invoice);

        Assert.Equal(41, invoice.Id);
        Assert.Equal(7, invoice.CompanyId);
        Assert.Equal("INV-SERVER", invoice.InvoiceNumber);
        Assert.Equal(CurrencyCode.USD, invoice.Currency);
        Assert.Equal(10m, invoice.Total);
        Assert.Equal(3m, invoice.DiscountAmount);
        Assert.Equal(4m, invoice.PaidAmount);
        Assert.Equal(1, invoice.DriverId);
        Assert.Equal(2, invoice.ActualDriverId);
        Assert.Equal("EXT-2", invoice.ExportInvoiceCode);
        Assert.Null(invoice.ExternalDriverName);
        Assert.Equal("VEH-2", invoice.VehicleNumber);
        Assert.Equal("Changed", invoice.Notes);
        Assert.Same(businessPartner, invoice.BusinessPartner);
        Assert.Same(store, invoice.Store);
        Assert.Same(country, invoice.Country);
        Assert.Same(responsibleDriver, invoice.Driver);
        Assert.Same(actualDriver, invoice.ActualDriver);
        Assert.Same(line, Assert.Single(invoice.Lines));
        Assert.Equal(rowVersion, invoice.RowVersion);
        Assert.Equal("creator", invoice.CreatedById);
        Assert.Equal(
            new DateTime(2026, 7, 25, 10, 30, 0, DateTimeKind.Utc),
            invoice.LastModifiedAt);
    }

    [Fact]
    public void MapsterCreate_DoesNotPopulateInvoiceCollectionsOrProtectedFields()
    {
        var request = CreateRequest(
            InvoiceType.Sales,
            PaymentTerm.Credit,
            containerStoreId: 3,
            lines: [new InvoiceLineRequest(1, 2, 1m, 10m, null)],
            containerLines: [new InvoiceContainerLineRequest(1, 1, 0)],
            driverId: 1,
            discountAmount: 3m,
            paidAmount: 17m) with
        {
            InvoiceNumber = "  INV-MAP  ",
            ActualDriverId = 2,
            ExportInvoiceCode = "  EXT-1  ",
            ExternalDriverName = "   ",
            VehicleNumber = "  VEH-1  ",
            Notes = "   "
        };

        var invoice = request.Adapt<Invoice>();

        Assert.Empty(invoice.Lines);
        Assert.Empty(invoice.ContainerLines);
        Assert.Equal(0, invoice.Id);
        Assert.Equal(0, invoice.CompanyId);
        Assert.Equal("INV-MAP", invoice.InvoiceNumber);
        Assert.Equal(0m, invoice.Total);
        Assert.Equal(InvoiceType.Sales, invoice.InvoiceType);
        Assert.Equal(PaymentTerm.Credit, invoice.PaymentTerm);
        Assert.Equal(request.InvoiceDate, invoice.InvoiceDate);
        Assert.Equal(request.BusinessPartnerId, invoice.BusinessPartnerId);
        Assert.Equal(request.StoreId, invoice.StoreId);
        Assert.Equal(request.ContainerStoreId, invoice.ContainerStoreId);
        Assert.Equal(1, invoice.DriverId);
        Assert.Equal(2, invoice.ActualDriverId);
        Assert.Equal(3m, invoice.DiscountAmount);
        Assert.Equal(17m, invoice.PaidAmount);
        Assert.Equal("EXT-1", invoice.ExportInvoiceCode);
        Assert.Null(invoice.ExternalDriverName);
        Assert.Equal("VEH-1", invoice.VehicleNumber);
        Assert.Null(invoice.Notes);
        Assert.Empty(invoice.RowVersion);
    }

    [Fact]
    public void MapsterResponses_MapNamesCalculationsAndOrderedChildren()
    {
        var itemUnit = new ItemUnit
        {
            Id = 1,
            CompanyId = 1,
            Name = "Kilogram"
        };
        var firstItem = new Item
        {
            Id = 1,
            CompanyId = 1,
            ItemUnitId = 1,
            ItemUnit = itemUnit,
            Code = "ITEM-1",
            Name = "First item"
        };
        var secondItem = new Item
        {
            Id = 2,
            CompanyId = 1,
            ItemUnitId = 1,
            ItemUnit = itemUnit,
            Code = "ITEM-2",
            Name = "Second item"
        };
        var firstLine = new InvoiceLine
        {
            Id = 1,
            CompanyId = 1,
            ItemId = 1,
            Item = firstItem,
            ItemUnitId = 1,
            ItemUnit = itemUnit,
            Count = 1,
            Weight = 1m,
            Price = 3m
        };
        var secondLine = new InvoiceLine
        {
            Id = 2,
            CompanyId = 1,
            ItemId = 2,
            Item = secondItem,
            ItemUnitId = 1,
            ItemUnit = itemUnit,
            Count = 1,
            Weight = 1m,
            Price = 4m
        };
        var firstContainer = new Container
        {
            Id = 1,
            CompanyId = 1,
            Code = "CONT-1",
            Name = "First container"
        };
        var secondContainer = new Container
        {
            Id = 2,
            CompanyId = 1,
            Code = "CONT-2",
            Name = "Second container"
        };
        var invoice = new Invoice
        {
            Id = 8,
            CompanyId = 1,
            InvoiceNumber = "INV-8",
            InvoiceType = InvoiceType.Sales,
            PaymentTerm = PaymentTerm.Credit,
            InvoiceDate = new DateOnly(2026, 7, 26),
            BusinessPartnerId = 1,
            BusinessPartner = new BusinessPartner
            {
                Id = 1,
                CompanyId = 1,
                Code = "BP-1",
                Name = "Partner"
            },
            StoreId = 1,
            Store = new Store
            {
                Id = 1,
                CompanyId = 1,
                Code = "STORE-1",
                Name = "Store"
            },
            DriverId = 1,
            Driver = new Driver
            {
                Id = 1,
                CompanyId = 1,
                Code = "DRV-1",
                Name = "Responsible Driver",
                LicenseNumber = "LIC-1"
            },
            ActualDriverId = 2,
            ActualDriver = new Driver
            {
                Id = 2,
                CompanyId = 1,
                Code = "DRV-2",
                Name = "Actual Driver",
                LicenseNumber = "LIC-2"
            },
            DiscountAmount = 2m,
            PaidAmount = 4m,
            Lines = [secondLine, firstLine],
            ContainerLines =
            [
                new InvoiceContainerLine
                {
                    Id = 2,
                    CompanyId = 1,
                    ContainerId = 2,
                    Container = secondContainer,
                    OutgoingUnits = 2
                },
                new InvoiceContainerLine
                {
                    Id = 1,
                    CompanyId = 1,
                    ContainerId = 1,
                    Container = firstContainer,
                    OutgoingUnits = 1
                }
            ]
        };
        invoice.CalculateTotal();

        var response = invoice.Adapt<InvoiceResponse>();
        var listResponse = invoice.Adapt<InvoiceListResponse>();

        Assert.Equal("Partner", response.BusinessPartnerName);
        Assert.Equal("Store", response.StoreName);
        Assert.Equal("Responsible Driver", response.DriverName);
        Assert.Equal("Actual Driver", response.ActualDriverName);
        Assert.Equal(7m, response.Subtotal);
        Assert.Equal(2m, response.DiscountAmount);
        Assert.Equal(4m, response.PaidAmount);
        Assert.Equal(1m, response.RemainingAmount);
        Assert.Equal([1, 2], response.Lines.Select(line => line.Id));
        Assert.Equal(
            [1, 2],
            response.ContainerLines.Select(line => line.Id));
        Assert.Equal("Partner", listResponse.BusinessPartnerName);
        Assert.Equal("Store", listResponse.StoreName);
        Assert.Equal("Responsible Driver", listResponse.DriverName);
        Assert.Equal("Actual Driver", listResponse.ActualDriverName);
        Assert.Equal(7m, listResponse.Subtotal);
        Assert.Equal(1m, listResponse.RemainingAmount);
        Assert.Equal(2, listResponse.LineCount);
        Assert.Equal(2, listResponse.ContainerLineCount);
    }

    [Fact]
    public void MapsterLineMapping_DoesNotOverwriteProtectedLineFields()
    {
        var line = new InvoiceLine
        {
            Id = 41,
            CompanyId = 7,
            InvoiceId = 9,
            ItemId = 1,
            ItemUnitId = 3,
            Count = 1,
            Weight = 2m,
            Price = 5m
        };
        line.CalculateAmounts();
        var originalQuantity = line.Quantity;
        var originalTotal = line.Total;

        new InvoiceLineRequest(1, 3, 4m, 7m, "updated").Adapt(line);

        Assert.Equal(41, line.Id);
        Assert.Equal(7, line.CompanyId);
        Assert.Equal(9, line.InvoiceId);
        Assert.Equal(3, line.ItemUnitId);
        Assert.Equal(3, line.Count);
        Assert.Equal(4m, line.Weight);
        Assert.Equal(7m, line.Price);
        Assert.Equal("updated", line.Notes);
        Assert.Equal(originalQuantity, line.Quantity);
        Assert.Equal(originalTotal, line.Total);
    }

    [Fact]
    public void CreateValidator_RequiresEnteredInvoiceNumber()
    {
        var request = CreateRequest(InvoiceType.SalesReturn) with
        {
            InvoiceNumber = "   "
        };

        var result = new InvoiceRequestValidator().Validate(request);

        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName == nameof(InvoiceRequest.InvoiceNumber));
    }

    [Fact]
    public void CreateValidator_UsesTrimmedInvoiceNumberLength()
    {
        var request = CreateRequest(InvoiceType.SalesReturn) with
        {
            InvoiceNumber =
                $"  {new string('N', InvoiceRequest.InvoiceNumberMaximumLength)}  "
        };

        var result = new InvoiceRequestValidator().Validate(request);

        Assert.DoesNotContain(
            result.Errors,
            error =>
                error.PropertyName == nameof(InvoiceRequest.InvoiceNumber));
    }

    [Fact]
    public void UpdateValidator_UsesTheEightByteRowVersionMessage()
    {
        var request = new InvoiceUpdateRequest(
            InvoiceType.Sales,
            PaymentTerm.Cash,
            new DateOnly(2026, 7, 25),
            null,
            1,
            1,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            0m,
            10m,
            null,
            [new InvoiceLineRequest(1, 1, 1m, 10m, null)],
            [],
            [1]);

        var result = new InvoiceUpdateRequestValidator().Validate(request);

        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage.Contains("8 بايت"));
    }

    [Fact]
    public void Validators_RejectDuplicateItemsAndContainers()
    {
        var line = new InvoiceLineRequest(1, 1, 1m, 10m, null);
        var container = new InvoiceContainerLineRequest(1, 1, 0);
        var request = CreateRequest(
            InvoiceType.SalesReturn,
            lines: [line, line],
            containerLines: [container, container]);

        var result = new InvoiceRequestValidator().Validate(request);

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(InvoiceRequest.Lines));
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(InvoiceRequest.ContainerLines));
    }

    private static InvoiceRequest CreateRequest(
        InvoiceType invoiceType,
        PaymentTerm paymentTerm = PaymentTerm.Cash,
        IReadOnlyList<InvoiceLineRequest>? lines = null,
        IReadOnlyList<InvoiceContainerLineRequest>? containerLines = null,
        DateOnly? invoiceDate = null,
        int storeId = 1,
        int? containerStoreId = null,
        int? driverId = null,
        decimal discountAmount = 0m,
        decimal? paidAmount = null,
        string? invoiceNumber = null)
    {
        var requestedLines =
            lines ?? [new InvoiceLineRequest(1, 2, 1m, 10m, null)];
        if (invoiceType == InvoiceType.SalesReturn)
        {
            requestedLines = requestedLines
                .Select(line =>
                    line.SourceInvoiceLineId.HasValue ||
                    line.ReturnUnitCost.HasValue
                        ? line
                        : line with
                        {
                            ReturnUnitCost = line.Price
                        })
                .ToArray();
        }

        var requestedPaidAmount = paidAmount ??
            (paymentTerm == PaymentTerm.Cash
                ? CalculateRequestTotal(requestedLines, discountAmount)
                : 0m);

        return new InvoiceRequest(
            invoiceNumber ?? $"INV-{Guid.NewGuid():N}",
            invoiceType,
            paymentTerm,
            invoiceDate ?? new DateOnly(2026, 7, 25),
            null,
            1,
            storeId,
            containerStoreId,
            null,
            driverId,
            null,
            false,
            null,
            null,
            null,
            discountAmount,
            requestedPaidAmount,
            null,
            requestedLines,
            containerLines ?? [],
            PartnerInvoiceNo: null,
            CashboxId: requestedPaidAmount > 0m ? 1 : null,
            CashMovementTypeId: requestedPaidAmount > 0m
                ? invoiceType is
                    InvoiceType.Sales or InvoiceType.PurchaseReturn
                    ? 1
                    : 2
                : null);
    }

    private static async Task SeedInvoiceFilterDataAsync(
        InvoiceTestDatabase database)
    {
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Countries (
                Id, Code, Name, ArabicName, IsActive, IsDeleted)
            VALUES (1, 'EG', 'Egypt', 'مصر', 1, 0);
            """);

        var service = database.CreateService();
        var missingPrice = CreateRequest(
            InvoiceType.SalesReturn,
            PaymentTerm.Cash,
            lines: [new InvoiceLineRequest(1, 1, 1m, 0m, null)],
            invoiceDate: new DateOnly(2026, 7, 10),
            storeId: 1,
            driverId: 1,
            invoiceNumber: "FILTER-SALES-001") with
        {
            BusinessPartnerId = 1,
            CountryId = 1
        };
        var priced = CreateRequest(
            InvoiceType.Purchase,
            PaymentTerm.Credit,
            lines: [new InvoiceLineRequest(2, 1, 1m, 15m, null)],
            invoiceDate: new DateOnly(2026, 7, 20),
            storeId: 2,
            driverId: 2,
            invoiceNumber: "FILTER-PURCHASE-002") with
        {
            BusinessPartnerId = 2
        };

        var first = await service.AddAsync(missingPrice);
        var second = await service.AddAsync(priced);

        Assert.True(
            first.IsSuccess,
            first.IsFailure ? first.Error.Description : null);
        Assert.True(
            second.IsSuccess,
            second.IsFailure ? second.Error.Description : null);
        database.Context.ChangeTracker.Clear();
    }

    private static InvoiceUpdateRequest CreateUpdateRequest(
        InvoiceResponse invoice,
        IReadOnlyList<InvoiceLineRequest> lines,
        int? storeId = null,
        InvoiceType? invoiceType = null,
        DateOnly? invoiceDate = null,
        IReadOnlyList<InvoiceContainerLineRequest>? containerLines = null,
        int? containerStoreId = null,
        int? driverId = null,
        PaymentTerm? paymentTerm = null,
        int? businessPartnerId = null,
        decimal? discountAmount = null,
        decimal? paidAmount = null)
    {
        var requestedInvoiceType = invoiceType ?? invoice.InvoiceType;
        if (requestedInvoiceType == InvoiceType.SalesReturn)
        {
            lines = lines
                .Select(line =>
                    line.SourceInvoiceLineId.HasValue ||
                    line.ReturnUnitCost.HasValue
                        ? line
                        : line with
                        {
                            ReturnUnitCost = line.Price
                        })
                .ToArray();
        }

        var requestedPaymentTerm = paymentTerm ?? invoice.PaymentTerm;
        var requestedDiscountAmount =
            discountAmount ?? invoice.DiscountAmount;
        var requestedTotal = CalculateRequestTotal(
            lines,
            requestedDiscountAmount);
        var requestedPaidAmount = paidAmount ??
            (requestedPaymentTerm == PaymentTerm.Cash
                ? requestedTotal
                : invoice.PaymentTerm == PaymentTerm.Credit &&
                  invoice.PaidAmount < requestedTotal
                    ? invoice.PaidAmount
                    : 0m);

        return new InvoiceUpdateRequest(
            requestedInvoiceType,
            requestedPaymentTerm,
            invoiceDate ?? invoice.InvoiceDate,
            invoice.DueDate,
            businessPartnerId ?? invoice.BusinessPartnerId,
            storeId ?? invoice.StoreId,
            containerStoreId ?? invoice.ContainerStoreId,
            invoice.CountryId,
            driverId ?? invoice.DriverId,
            invoice.ActualDriverId,
            invoice.UsesExternalDriver,
            invoice.ExternalDriverName,
            invoice.VehicleNumber,
            invoice.ExportInvoiceCode,
            requestedDiscountAmount,
            requestedPaidAmount,
            invoice.Notes,
            lines,
            containerLines ?? [],
            invoice.RowVersion,
            PartnerInvoiceNo: invoice.PartnerInvoiceNo,
            CashboxId: requestedPaidAmount > 0m ? 1 : null,
            CashMovementTypeId: requestedPaidAmount > 0m
                ? requestedInvoiceType is
                    InvoiceType.Sales or InvoiceType.PurchaseReturn
                    ? 1
                    : 2
                : null);
    }

    private static decimal CalculateRequestTotal(
        IReadOnlyList<InvoiceLineRequest> lines,
        decimal discountAmount)
    {
        var subtotal = 0m;
        foreach (var line in lines)
        {
            if (!InvoiceAmountRules.TryCalculate(
                    line.Count,
                    line.Weight,
                    line.Price,
                    out _,
                    out var lineTotal))
            {
                // Leave the header amount at zero so the service reaches its
                // line-calculation validation for intentionally invalid lines.
                return 0m;
            }

            subtotal += lineTotal;
        }

        return decimal.Round(
            subtotal - discountAmount,
            InvoiceAmountRules.MoneyScale,
            MidpointRounding.AwayFromZero);
    }

    private static async Task AddMovementAsync(
        InvoiceTestDatabase database,
        int storeId,
        int itemId,
        ItemMovementType movementType,
        int referenceId,
        string referenceNumber,
        DateOnly movementDate,
        decimal quantityIn = 0m,
        decimal quantityOut = 0m,
        int companyId = 1)
    {
        var movement = new ItemMovement
        {
            CompanyId = companyId,
            StoreId = storeId,
            ItemId = itemId,
            ItemUnitId = 1,
            MovementType = movementType,
            ReferenceId = referenceId,
            ReferenceNumber = referenceNumber,
            MovementDate = movementDate,
            QuantityIn = quantityIn,
            QuantityOut = quantityOut
        };
        if (quantityIn > 0m)
        {
            CostedMovement(movement);
        }

        database.Context.ItemMovements.Add(movement);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
    }

    private static ItemMovement CostedMovement(
        ItemMovement movement,
        decimal unitCost = 10m)
    {
        movement.ApplyCostSnapshot(
            InventoryCostStatus.Final,
            0m,
            unitCost,
            movement.QuantityIn * unitCost,
            movement.QuantityIn,
            unitCost,
            movement.QuantityIn * unitCost);
        return movement;
    }

    private static async Task<Error?> InvokeValidateStockAsync(
        InvoiceService service,
        Invoice invoice,
        IReadOnlyList<InvoiceLineRequest> lines,
        int? currentInvoiceId,
        string? currentInvoiceNumber)
    {
        var method = typeof(InvoiceService).GetMethod(
            "ValidateStockAsync",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var invocation = method.Invoke(
            service,
            new object?[]
            {
                invoice,
                lines,
                currentInvoiceId,
                currentInvoiceNumber,
                CancellationToken.None
            });
        var task = Assert.IsType<Task<Error?>>(invocation);
        return await task;
    }

    private sealed class InvoiceTestDatabase : IAsyncDisposable
    {
        private InvoiceTestDatabase(
            SqliteConnection connection,
            ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        public ApplicationDbContext Context { get; }

        public static async Task<InvoiceTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var auditInterceptor = new AuditableEntityInterceptor(
                new HttpContextAccessor(),
                TimeProvider.System);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(auditInterceptor)
                .Options;
            var context = new ApplicationDbContext(options);

            await CreateSchemaAsync(context);
            await SeedAsync(context);

            return new InvoiceTestDatabase(connection, context);
        }

        public InvoiceService CreateService()
        {
            var companyContext = new TestCurrentCompanyContext(1);

            return new InvoiceService(
                Context,
                new PaginationService(),
                companyContext,
                new MiniErp.Tests.TestExchangeRateResolver(),
                new InventoryStockService(Context, companyContext),
                new InventoryCostingService(
                    Context,
                    companyContext,
                    TimeProvider.System),
                TimeProvider.System);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }

        private static async Task CreateSchemaAsync(
            ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE Companies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Address TEXT NOT NULL,
                    CommercialRegister TEXT NOT NULL,
                    TaxNumber TEXT NOT NULL,
                    ManagerName TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE CompanySettings (
                    CompanyId INTEGER NOT NULL PRIMARY KEY,
                    BaseCurrency INTEGER NOT NULL DEFAULT 1,
                    StockBalanceCheckMode INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE
                );

                CREATE TABLE BusinessPartners (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Currency INTEGER NOT NULL,
                    CreditLimit NUMERIC NOT NULL,
                    IsActive INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Stores (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Address TEXT NULL,
                    IsContainerStore INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL,
                    CreatedById TEXT NOT NULL DEFAULT '',
                    CreatedOn TEXT NOT NULL DEFAULT '2026-01-01',
                    CreatedByPc TEXT NOT NULL DEFAULT '',
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL
                );

                CREATE TABLE ItemUnits (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    IsActive INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Items (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    ItemUnitId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    IsActive INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Countries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    ArabicName TEXT NOT NULL,
                    IsActive INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Drivers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    PhoneNumber TEXT NULL,
                    NationalId TEXT NULL,
                    LicenseNumber TEXT NOT NULL,
                    LicenseExpiryDate TEXT NULL,
                    IsActive INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Containers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    IsActive INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StoreContainers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    ContainerId INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StockOpeningBalances (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    DocumentDate TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StockOpeningBalanceLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    StockOpeningBalanceId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    Quantity NUMERIC NOT NULL,
                    Price NUMERIC NOT NULL DEFAULT 0,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Invoices (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    InvoiceNumber TEXT NOT NULL,
                    ExportInvoiceCode TEXT NULL,
                    PartnerInvoiceNo TEXT NULL,
                    InvoiceType INTEGER NOT NULL,
                    ContentType INTEGER NOT NULL DEFAULT 1,
                    PaymentTerm INTEGER NOT NULL DEFAULT 1,
                    InvoiceDate TEXT NOT NULL,
                    DueDate TEXT NULL,
                    BusinessPartnerId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    ContainerStoreId INTEGER NULL,
                    CountryId INTEGER NULL,
                    Currency INTEGER NOT NULL,
                    ExchangeRateId INTEGER NULL,
                    ExchangeRate NUMERIC NOT NULL DEFAULT 1,
                    DriverId INTEGER NULL,
                    ActualDriverId INTEGER NULL,
                    UsesExternalDriver INTEGER NOT NULL DEFAULT 0,
                    ExternalDriverName TEXT NULL,
                    VehicleNumber TEXT NULL,
                    Total NUMERIC NOT NULL,
                    DiscountAmount NUMERIC NOT NULL DEFAULT 0,
                    WBWeight NUMERIC NOT NULL DEFAULT 0,
                    WBScaleDifference NUMERIC NOT NULL DEFAULT 0,
                    WBDiscount NUMERIC NOT NULL DEFAULT 0,
                    WBTotal NUMERIC NOT NULL DEFAULT 0,
                    PaidAmount NUMERIC NOT NULL DEFAULT 0,
                    BaseSubtotal NUMERIC NOT NULL DEFAULT 0,
                    BaseDiscountAmount NUMERIC NOT NULL DEFAULT 0,
                    BaseTotal NUMERIC NOT NULL DEFAULT 0,
                    BasePaidAmountAtInvoiceRate NUMERIC NOT NULL DEFAULT 0,
                    Notes TEXT NULL,
                    LastModifiedAt TEXT NOT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE InvoiceLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    InvoiceId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    ItemUnitId INTEGER NOT NULL,
                    SourceInvoiceLineId INTEGER NULL,
                    ReturnUnitCost NUMERIC NULL,
                    Count INTEGER NOT NULL,
                    Weight NUMERIC NOT NULL,
                    Quantity NUMERIC NOT NULL,
                    Price NUMERIC NOT NULL,
                    Total NUMERIC NOT NULL,
                    BaseUnitPrice NUMERIC NOT NULL DEFAULT 0,
                    BaseTotal NUMERIC NOT NULL DEFAULT 0,
                    Notes TEXT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE InvoiceContainerLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    InvoiceId INTEGER NOT NULL,
                    ContainerId INTEGER NOT NULL,
                    OutgoingUnits INTEGER NOT NULL,
                    IncomingUnits INTEGER NOT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE ItemMovements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    ItemUnitId INTEGER NULL,
                    MovementType INTEGER NOT NULL,
                    ReferenceId INTEGER NOT NULL,
                    ReferenceNumber TEXT NOT NULL,
                    MovementDate TEXT NOT NULL,
                    QuantityIn NUMERIC NOT NULL,
                    QuantityOut NUMERIC NOT NULL,
                    CostStatus INTEGER NOT NULL DEFAULT 1,
                    PendingCostQuantity NUMERIC NOT NULL DEFAULT 0,
                    UnitCost NUMERIC NULL,
                    TotalCost NUMERIC NOT NULL DEFAULT 0,
                    QuantityAfter NUMERIC NOT NULL DEFAULT 0,
                    AverageCostAfter NUMERIC NOT NULL DEFAULT 0,
                    InventoryValueAfter NUMERIC NOT NULL DEFAULT 0,
                    Description TEXT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE StockAdjustmentLines (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    StockAdjustmentId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    UnitCost NUMERIC NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE UNIQUE INDEX UX_ItemMovements_Company_Id
                ON ItemMovements (CompanyId, Id);

                CREATE TABLE InventoryCostAllocations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    OutboundMovementId INTEGER NOT NULL,
                    InboundMovementId INTEGER NOT NULL,
                    Quantity NUMERIC NOT NULL,
                    UnitCost NUMERIC NOT NULL,
                    TotalCost NUMERIC NOT NULL,
                    CreatedOn TEXT NOT NULL
                );

                CREATE UNIQUE INDEX UX_InventoryCostAllocations_Pair
                ON InventoryCostAllocations (
                    CompanyId,
                    OutboundMovementId,
                    InboundMovementId);

                CREATE TABLE ItemStoreBalances (
                    CompanyId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    ItemId INTEGER NOT NULL,
                    Quantity NUMERIC NOT NULL DEFAULT 0,
                    AverageCost NUMERIC NOT NULL DEFAULT 0,
                    InventoryValue NUMERIC NOT NULL DEFAULT 0,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL,
                    PRIMARY KEY (CompanyId, StoreId, ItemId)
                );

                CREATE TABLE ContainerMovements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NOT NULL,
                    ContainerStoreId INTEGER NOT NULL,
                    ContainerId INTEGER NOT NULL,
                    InvoiceId INTEGER NOT NULL,
                    InvoiceNumber TEXT NOT NULL,
                    MovementDate TEXT NOT NULL,
                    OutgoingUnits INTEGER NOT NULL,
                    IncomingUnits INTEGER NOT NULL,
                    Description TEXT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE BusinessPartnerMovements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NOT NULL,
                    InvoiceId INTEGER NULL,
                    CashVoucherId INTEGER NULL,
                    MovementType INTEGER NOT NULL,
                    MovementDate TEXT NOT NULL,
                    Currency INTEGER NOT NULL,
                    Debit NUMERIC NOT NULL,
                    Credit NUMERIC NOT NULL,
                    ExchangeRate NUMERIC NOT NULL DEFAULT 1,
                    BaseDebit NUMERIC NOT NULL DEFAULT 0,
                    BaseCredit NUMERIC NOT NULL DEFAULT 0,
                    Description TEXT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE DriverTrips (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    DriverId INTEGER NOT NULL,
                    ActualDriverId INTEGER NULL,
                    InvoiceId INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NOT NULL,
                    InvoiceNumber TEXT NOT NULL,
                    ExportInvoiceCode TEXT NULL,
                    TripDate TEXT NOT NULL,
                    Price NUMERIC NULL,
                    Cost NUMERIC NULL,
                    CostNotes TEXT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE Cashboxes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Currency INTEGER NOT NULL,
                    OpeningBalance NUMERIC NOT NULL,
                    OpeningBalanceDate TEXT NOT NULL DEFAULT '2026-01-01',
                    OpeningExchangeRateId INTEGER NULL,
                    OpeningExchangeRate NUMERIC NOT NULL DEFAULT 1,
                    BaseOpeningBalance NUMERIC NOT NULL DEFAULT 0,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    Notes TEXT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE CashMovementTypes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Direction INTEGER NOT NULL,
                    PartnerEffect INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    Notes TEXT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TABLE CashVouchers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    InvoiceId INTEGER NULL,
                    VoucherNumber TEXT NOT NULL,
                    VoucherDate TEXT NOT NULL,
                    Direction INTEGER NOT NULL,
                    CashboxId INTEGER NOT NULL,
                    CashMovementTypeId INTEGER NOT NULL,
                    PartyType INTEGER NOT NULL,
                    BusinessPartnerId INTEGER NULL,
                    DriverId INTEGER NULL,
                    DriverTripId INTEGER NULL,
                    ExternalPartyName TEXT NULL,
                    Amount NUMERIC NOT NULL,
                    Currency INTEGER NOT NULL,
                    ExchangeRateId INTEGER NULL,
                    ExchangeRate NUMERIC NOT NULL DEFAULT 1,
                    BaseAmount NUMERIC NOT NULL DEFAULT 0,
                    ReferenceNumber TEXT NULL,
                    Description TEXT NULL,
                    Notes TEXT NULL,
                    LastModifiedAt TEXT NOT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE UNIQUE INDEX UX_CashVouchers_Invoice
                ON CashVouchers (CompanyId, InvoiceId)
                WHERE InvoiceId IS NOT NULL AND IsDeleted = 0;

                CREATE TABLE InvoicePayments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CompanyId INTEGER NOT NULL,
                    InvoiceId INTEGER NOT NULL,
                    CashVoucherId INTEGER NOT NULL,
                    InvoiceCurrency INTEGER NOT NULL,
                    AppliedAmount NUMERIC NOT NULL,
                    CashboxCurrency INTEGER NOT NULL,
                    CashboxAmount NUMERIC NOT NULL,
                    InvoiceToBaseRate NUMERIC NOT NULL,
                    CashboxToBaseRate NUMERIC NOT NULL,
                    AppliedBaseAmount NUMERIC NOT NULL,
                    CashboxBaseAmount NUMERIC NOT NULL,
                    RealizedExchangeDifference NUMERIC NOT NULL,
                    CreatedById TEXT NOT NULL,
                    CreatedOn TEXT NOT NULL,
                    CreatedByPc TEXT NOT NULL,
                    UpdatedById TEXT NULL,
                    UpdatedOn TEXT NULL,
                    UpdatedByPc TEXT NULL,
                    DeletedById TEXT NULL,
                    DeletedOn TEXT NULL,
                    DeletedByPc TEXT NULL,
                    IsDeleted INTEGER NOT NULL
                );

                CREATE TRIGGER AdvanceInvoiceRowVersion
                AFTER UPDATE ON Invoices
                BEGIN
                    UPDATE Invoices
                    SET RowVersion = randomblob(8)
                    WHERE Id = NEW.Id;
                END;

                CREATE TRIGGER AdvanceDriverTripRowVersion
                AFTER UPDATE ON DriverTrips
                BEGIN
                    UPDATE DriverTrips
                    SET RowVersion = randomblob(8)
                    WHERE Id = NEW.Id;
                END;

                CREATE TRIGGER AdvanceCashVoucherRowVersion
                AFTER UPDATE ON CashVouchers
                BEGIN
                    UPDATE CashVouchers
                    SET RowVersion = randomblob(8)
                    WHERE Id = NEW.Id;
                END;
                """);
        }

        private static async Task SeedAsync(ApplicationDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Companies (
                    Id, Name, Address, CommercialRegister, TaxNumber,
                    ManagerName, IsDeleted)
                VALUES (1, 'Company', '', 'CR', 'TX', 'Manager', 0);

                INSERT INTO BusinessPartners (
                    Id, CompanyId, Code, Name, Currency, CreditLimit,
                    IsActive, IsDeleted)
                VALUES
                    (1, 1, 'BP-1', 'Partner', 1, 10000, 1, 0),
                    (2, 1, 'BP-2', 'Second Partner', 1, 10000, 1, 0);

                INSERT INTO Stores (
                    Id, CompanyId, BusinessPartnerId, Code, Name, Address,
                    IsContainerStore, IsActive, IsDeleted)
                VALUES
                    (1, 1, NULL, 'STORE-1', 'Product Store', NULL, 0, 1, 0),
                    (2, 1, NULL, 'STORE-2', 'Second Product Store', NULL, 0, 1, 0),
                    (3, 1, 1, 'CONTAINER-STORE', 'Container Store', NULL, 1, 1, 0);

                INSERT INTO ItemUnits (
                    Id, CompanyId, Name, IsActive, IsDeleted)
                VALUES (1, 1, 'Unit', 1, 0);

                INSERT INTO Items (
                    Id, CompanyId, ItemUnitId, Code, Name, Description,
                    IsActive, IsDeleted)
                VALUES
                    (1, 1, 1, 'ITEM-1', 'Item 1', NULL, 1, 0),
                    (2, 1, 1, 'ITEM-2', 'Item 2', NULL, 1, 0);

                INSERT INTO Containers (
                    Id, CompanyId, Code, Name, Description, IsActive, IsDeleted)
                VALUES (1, 1, 'CONT-1', 'Container 1', NULL, 1, 0);

                INSERT INTO StoreContainers (
                    Id, CompanyId, StoreId, ContainerId, IsActive, IsDeleted)
                VALUES (1, 1, 3, 1, 1, 0);

                INSERT INTO Drivers (
                    Id, CompanyId, Code, Name, PhoneNumber, NationalId,
                    LicenseNumber, LicenseExpiryDate, IsActive, IsDeleted)
                VALUES
                    (1, 1, 'DRV-1', 'Driver 1', NULL, NULL, 'LIC-1', NULL, 1, 0),
                    (2, 1, 'DRV-2', 'Driver 2', NULL, NULL, 'LIC-2', NULL, 1, 0),
                    (3, 1, 'DRV-3', 'Inactive Driver', NULL, NULL, 'LIC-3', NULL, 0, 0),
                    (4, 2, 'DRV-4', 'Other Company Driver', NULL, NULL, 'LIC-4', NULL, 1, 0);

                INSERT INTO Cashboxes (
                    Id, CompanyId, Code, Name, Currency, OpeningBalance,
                    IsActive, CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 'MAIN', 'Main Cashbox', 1, 10000, 1,
                     'test', '2026-01-01', 'test', 0);

                INSERT INTO CashMovementTypes (
                    Id, CompanyId, Name, Direction, PartnerEffect, IsActive,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                VALUES
                    (1, 1, 'Customer Collection', 1, 2, 1,
                     'test', '2026-01-01', 'test', 0),
                    (2, 1, 'Supplier Payment', 2, 1, 1,
                     'test', '2026-01-01', 'test', 0);

                INSERT INTO StockOpeningBalances (
                    Id, CompanyId, StoreId, DocumentDate, IsDeleted)
                VALUES (1, 1, 1, '2026-01-01', 0);

                INSERT INTO StockOpeningBalanceLines (
                    Id, CompanyId, StockOpeningBalanceId, ItemId, Quantity,
                    IsDeleted)
                VALUES
                    (1, 1, 1, 1, 10, 0),
                    (2, 1, 1, 2, 10, 0);
                """);
        }
    }

    private sealed record TestCurrentCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
