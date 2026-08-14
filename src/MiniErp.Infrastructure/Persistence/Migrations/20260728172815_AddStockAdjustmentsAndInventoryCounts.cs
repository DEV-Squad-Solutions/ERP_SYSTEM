using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAdjustmentsAndInventoryCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CountDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SnapshotTakenAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ReconciledAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
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
                    table.PrimaryKey("PK_InventoryCounts", x => x.Id);
                    table.UniqueConstraint("AK_InventoryCounts_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_InventoryCounts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCounts_Stores_CompanyId_StoreId",
                        columns: x => new { x.CompanyId, x.StoreId },
                        principalTable: "Stores",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCountLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    InventoryCountId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ItemUnitId = table.Column<int>(type: "int", nullable: false),
                    SystemQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PhysicalQuantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_InventoryCountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryCountLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCountLines_InventoryCounts_CompanyId_InventoryCountId",
                        columns: x => new { x.CompanyId, x.InventoryCountId },
                        principalTable: "InventoryCounts",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCountLines_ItemUnits_CompanyId_ItemUnitId",
                        columns: x => new { x.CompanyId, x.ItemUnitId },
                        principalTable: "ItemUnits",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCountLines_Items_CompanyId_ItemId",
                        columns: x => new { x.CompanyId, x.ItemId },
                        principalTable: "Items",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourceInventoryCountId = table.Column<int>(type: "int", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
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
                    table.PrimaryKey("PK_StockAdjustments", x => x.Id);
                    table.UniqueConstraint("AK_StockAdjustments_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_InventoryCounts_CompanyId_SourceInventoryCountId",
                        columns: x => new { x.CompanyId, x.SourceInventoryCountId },
                        principalTable: "InventoryCounts",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Stores_CompanyId_StoreId",
                        columns: x => new { x.CompanyId, x.StoreId },
                        principalTable: "Stores",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    StockAdjustmentId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ItemUnitId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_StockAdjustmentLines", x => x.Id);
                    table.CheckConstraint("CK_StockAdjustmentLines_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_ItemUnits_CompanyId_ItemUnitId",
                        columns: x => new { x.CompanyId, x.ItemUnitId },
                        principalTable: "ItemUnits",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_Items_CompanyId_ItemId",
                        columns: x => new { x.CompanyId, x.ItemId },
                        principalTable: "Items",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLines_StockAdjustments_CompanyId_StockAdjustmentId",
                        columns: x => new { x.CompanyId, x.StockAdjustmentId },
                        principalTable: "StockAdjustments",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_CompanyId_InventoryCountId_ItemId",
                table: "InventoryCountLines",
                columns: new[] { "CompanyId", "InventoryCountId", "ItemId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_CompanyId_ItemId",
                table: "InventoryCountLines",
                columns: new[] { "CompanyId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_CompanyId_ItemUnitId",
                table: "InventoryCountLines",
                columns: new[] { "CompanyId", "ItemUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_CompanyId_DocumentNumber",
                table: "InventoryCounts",
                columns: new[] { "CompanyId", "DocumentNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_CompanyId_StoreId",
                table: "InventoryCounts",
                columns: new[] { "CompanyId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_CompanyId_ItemId",
                table: "StockAdjustmentLines",
                columns: new[] { "CompanyId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_CompanyId_ItemUnitId",
                table: "StockAdjustmentLines",
                columns: new[] { "CompanyId", "ItemUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLines_CompanyId_StockAdjustmentId_ItemId",
                table: "StockAdjustmentLines",
                columns: new[] { "CompanyId", "StockAdjustmentId", "ItemId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_CompanyId_DocumentNumber",
                table: "StockAdjustments",
                columns: new[] { "CompanyId", "DocumentNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_CompanyId_StoreId",
                table: "StockAdjustments",
                columns: new[] { "CompanyId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "UX_StockAdjustments_CompanyId_SourceInventoryCountId_Direction",
                table: "StockAdjustments",
                columns: new[] { "CompanyId", "SourceInventoryCountId", "Direction" },
                unique: true,
                filter: "[SourceInventoryCountId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryCountLines");

            migrationBuilder.DropTable(
                name: "StockAdjustmentLines");

            migrationBuilder.DropTable(
                name: "StockAdjustments");

            migrationBuilder.DropTable(
                name: "InventoryCounts");
        }
    }
}
