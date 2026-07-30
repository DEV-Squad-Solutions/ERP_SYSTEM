using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class modifyInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemMovements_CompanyId_StoreId_ItemId_MovementDate_Id",
                table: "ItemMovements");

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "StockAdjustmentLines",
                type: "decimal(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageCostAfter",
                table: "ItemMovements",
                type: "decimal(24,8)",
                precision: 24,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CostStatus",
                table: "ItemMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "InventoryValueAfter",
                table: "ItemMovements",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingCostQuantity",
                table: "ItemMovements",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityAfter",
                table: "ItemMovements",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "ItemMovements",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "ItemMovements",
                type: "decimal(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerInvoiceNo",
                table: "Invoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnUnitCost",
                table: "InvoiceLines",
                type: "decimal(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceInvoiceLineId",
                table: "InvoiceLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceId",
                table: "CashVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryCostAllocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    OutboundMovementId = table.Column<int>(type: "int", nullable: false),
                    InboundMovementId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(24,8)", precision: 24, scale: 8, nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostAllocations", x => x.Id);
                    table.CheckConstraint("CK_InventoryCostAllocations_Cost_NonNegative", "[UnitCost] >= 0 AND [TotalCost] >= 0");
                    table.CheckConstraint("CK_InventoryCostAllocations_DifferentMovements", "[OutboundMovementId] <> [InboundMovementId]");
                    table.CheckConstraint("CK_InventoryCostAllocations_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryCostAllocations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostAllocations_ItemMovements_CompanyId_InboundMovementId",
                        columns: x => new { x.CompanyId, x.InboundMovementId },
                        principalTable: "ItemMovements",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostAllocations_ItemMovements_CompanyId_OutboundMovementId",
                        columns: x => new { x.CompanyId, x.OutboundMovementId },
                        principalTable: "ItemMovements",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostAllocations_Items_CompanyId_ItemId",
                        columns: x => new { x.CompanyId, x.ItemId },
                        principalTable: "Items",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostAllocations_Stores_CompanyId_StoreId",
                        columns: x => new { x.CompanyId, x.StoreId },
                        principalTable: "Stores",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemStoreBalances",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    AverageCost = table.Column<decimal>(type: "decimal(24,8)", precision: 24, scale: 8, nullable: false),
                    InventoryValue = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeletedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemStoreBalances", x => new { x.CompanyId, x.StoreId, x.ItemId });
                    table.CheckConstraint("CK_ItemStoreBalances_Costs_NonNegative", "[AverageCost] >= 0 AND [InventoryValue] >= 0");
                    table.CheckConstraint("CK_ItemStoreBalances_NonPositiveState", "[Quantity] > 0 OR ([AverageCost] = 0 AND [InventoryValue] = 0)");
                    table.ForeignKey(
                        name: "FK_ItemStoreBalances_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemStoreBalances_Items_CompanyId_ItemId",
                        columns: x => new { x.CompanyId, x.ItemId },
                        principalTable: "Items",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemStoreBalances_Stores_CompanyId_StoreId",
                        columns: x => new { x.CompanyId, x.StoreId },
                        principalTable: "Stores",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovements_CompanyId_StoreId_ItemId_CostStatus_MovementDate_CreatedOn_Id",
                table: "ItemMovements",
                columns: new[] { "CompanyId", "StoreId", "ItemId", "CostStatus", "MovementDate", "CreatedOn", "Id" },
                filter: "[IsDeleted] = 0 AND [CostStatus] IN (2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovements_CompanyId_StoreId_ItemId_MovementDate_CreatedOn_Id",
                table: "ItemMovements",
                columns: new[] { "CompanyId", "StoreId", "ItemId", "MovementDate", "CreatedOn", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemMovements_Costs_NonNegative",
                table: "ItemMovements",
                sql: "[PendingCostQuantity] >= 0 AND [TotalCost] >= 0 AND [AverageCostAfter] >= 0 AND [InventoryValueAfter] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemMovements_NonPositiveState",
                table: "ItemMovements",
                sql: "[QuantityAfter] > 0 OR ([AverageCostAfter] = 0 AND [InventoryValueAfter] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemMovements_PendingWithinOutbound",
                table: "ItemMovements",
                sql: "[PendingCostQuantity] <= [QuantityOut]");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_CompanyId_SourceInvoiceLineId",
                table: "InvoiceLines",
                columns: new[] { "CompanyId", "SourceInvoiceLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_InvoiceId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "InvoiceId" },
                unique: true,
                filter: "[InvoiceId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostAllocations_CompanyId_InboundMovementId",
                table: "InventoryCostAllocations",
                columns: new[] { "CompanyId", "InboundMovementId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostAllocations_CompanyId_ItemId",
                table: "InventoryCostAllocations",
                columns: new[] { "CompanyId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostAllocations_CompanyId_OutboundMovementId_InboundMovementId",
                table: "InventoryCostAllocations",
                columns: new[] { "CompanyId", "OutboundMovementId", "InboundMovementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostAllocations_CompanyId_StoreId_ItemId_InboundMovementId",
                table: "InventoryCostAllocations",
                columns: new[] { "CompanyId", "StoreId", "ItemId", "InboundMovementId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostAllocations_CompanyId_StoreId_ItemId_OutboundMovementId",
                table: "InventoryCostAllocations",
                columns: new[] { "CompanyId", "StoreId", "ItemId", "OutboundMovementId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemStoreBalances_CompanyId_ItemId",
                table: "ItemStoreBalances",
                columns: new[] { "CompanyId", "ItemId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CashVouchers_Invoices_CompanyId_InvoiceId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "InvoiceId" },
                principalTable: "Invoices",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLines_InvoiceLines_CompanyId_SourceInvoiceLineId",
                table: "InvoiceLines",
                columns: new[] { "CompanyId", "SourceInvoiceLineId" },
                principalTable: "InvoiceLines",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashVouchers_Invoices_CompanyId_InvoiceId",
                table: "CashVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLines_InvoiceLines_CompanyId_SourceInvoiceLineId",
                table: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "InventoryCostAllocations");

            migrationBuilder.DropTable(
                name: "ItemStoreBalances");

            migrationBuilder.DropIndex(
                name: "IX_ItemMovements_CompanyId_StoreId_ItemId_CostStatus_MovementDate_CreatedOn_Id",
                table: "ItemMovements");

            migrationBuilder.DropIndex(
                name: "IX_ItemMovements_CompanyId_StoreId_ItemId_MovementDate_CreatedOn_Id",
                table: "ItemMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemMovements_Costs_NonNegative",
                table: "ItemMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemMovements_NonPositiveState",
                table: "ItemMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemMovements_PendingWithinOutbound",
                table: "ItemMovements");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_CompanyId_SourceInvoiceLineId",
                table: "InvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_CompanyId_InvoiceId",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "StockAdjustmentLines");

            migrationBuilder.DropColumn(
                name: "AverageCostAfter",
                table: "ItemMovements");

            migrationBuilder.DropColumn(
                name: "CostStatus",
                table: "ItemMovements");

            migrationBuilder.DropColumn(
                name: "InventoryValueAfter",
                table: "ItemMovements");

            migrationBuilder.DropColumn(
                name: "PendingCostQuantity",
                table: "ItemMovements");

            migrationBuilder.DropColumn(
                name: "QuantityAfter",
                table: "ItemMovements");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "ItemMovements");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "ItemMovements");

            migrationBuilder.DropColumn(
                name: "PartnerInvoiceNo",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReturnUnitCost",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "SourceInvoiceLineId",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "CashVouchers");

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovements_CompanyId_StoreId_ItemId_MovementDate_Id",
                table: "ItemMovements",
                columns: new[] { "CompanyId", "StoreId", "ItemId", "MovementDate", "Id" });
        }
    }
}
