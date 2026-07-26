using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessPartnerMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BusinessPartnerId = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_BusinessPartnerMovements", x => x.Id);
                    table.UniqueConstraint("AK_BusinessPartnerMovements_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_BusinessPartnerMovements_Amounts_NonNegative", "[Debit] >= 0 AND [Credit] >= 0");
                    table.CheckConstraint("CK_BusinessPartnerMovements_ExactlyOneAmount", "([Debit] > 0 AND [Credit] = 0) OR ([Debit] = 0 AND [Credit] > 0)");
                    table.ForeignKey(
                        name: "FK_BusinessPartnerMovements_BusinessPartners_CompanyId_BusinessPartnerId",
                        columns: x => new { x.CompanyId, x.BusinessPartnerId },
                        principalTable: "BusinessPartners",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerMovements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerMovements_Invoices_CompanyId_InvoiceId",
                        columns: x => new { x.CompanyId, x.InvoiceId },
                        principalTable: "Invoices",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContainerMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BusinessPartnerId = table.Column<int>(type: "int", nullable: false),
                    ContainerStoreId = table.Column<int>(type: "int", nullable: false),
                    ContainerId = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OutgoingUnits = table.Column<int>(type: "int", nullable: false),
                    IncomingUnits = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ContainerMovements", x => x.Id);
                    table.UniqueConstraint("AK_ContainerMovements_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_ContainerMovements_Units_NonNegative", "[OutgoingUnits] >= 0 AND [IncomingUnits] >= 0");
                    table.CheckConstraint("CK_ContainerMovements_Units_NotBothZero", "[OutgoingUnits] > 0 OR [IncomingUnits] > 0");
                    table.ForeignKey(
                        name: "FK_ContainerMovements_BusinessPartners_CompanyId_BusinessPartnerId",
                        columns: x => new { x.CompanyId, x.BusinessPartnerId },
                        principalTable: "BusinessPartners",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerMovements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerMovements_Containers_CompanyId_ContainerId",
                        columns: x => new { x.CompanyId, x.ContainerId },
                        principalTable: "Containers",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerMovements_Invoices_CompanyId_InvoiceId",
                        columns: x => new { x.CompanyId, x.InvoiceId },
                        principalTable: "Invoices",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerMovements_Stores_CompanyId_ContainerStoreId",
                        columns: x => new { x.CompanyId, x.ContainerStoreId },
                        principalTable: "Stores",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DriverTrips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    BusinessPartnerId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExportInvoiceCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TripDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_DriverTrips", x => x.Id);
                    table.UniqueConstraint("AK_DriverTrips_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_DriverTrips_BusinessPartners_CompanyId_BusinessPartnerId",
                        columns: x => new { x.CompanyId, x.BusinessPartnerId },
                        principalTable: "BusinessPartners",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverTrips_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverTrips_Drivers_CompanyId_DriverId",
                        columns: x => new { x.CompanyId, x.DriverId },
                        principalTable: "Drivers",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverTrips_Invoices_CompanyId_InvoiceId",
                        columns: x => new { x.CompanyId, x.InvoiceId },
                        principalTable: "Invoices",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ItemUnitId = table.Column<int>(type: "int", nullable: true),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    ReferenceId = table.Column<int>(type: "int", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    QuantityIn = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    QuantityOut = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ItemMovements", x => x.Id);
                    table.UniqueConstraint("AK_ItemMovements_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_ItemMovements_ExactlyOneDirection", "([QuantityIn] > 0 AND [QuantityOut] = 0) OR ([QuantityIn] = 0 AND [QuantityOut] > 0)");
                    table.CheckConstraint("CK_ItemMovements_Quantity_NonNegative", "[QuantityIn] >= 0 AND [QuantityOut] >= 0");
                    table.ForeignKey(
                        name: "FK_ItemMovements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemMovements_ItemUnits_CompanyId_ItemUnitId",
                        columns: x => new { x.CompanyId, x.ItemUnitId },
                        principalTable: "ItemUnits",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemMovements_Items_CompanyId_ItemId",
                        columns: x => new { x.CompanyId, x.ItemId },
                        principalTable: "Items",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemMovements_Stores_CompanyId_StoreId",
                        columns: x => new { x.CompanyId, x.StoreId },
                        principalTable: "Stores",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerMovements_CompanyId_BusinessPartnerId_Currency_MovementDate_Id",
                table: "BusinessPartnerMovements",
                columns: new[] { "CompanyId", "BusinessPartnerId", "Currency", "MovementDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerMovements_CompanyId_InvoiceId",
                table: "BusinessPartnerMovements",
                columns: new[] { "CompanyId", "InvoiceId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_CompanyId_BusinessPartnerId_ContainerId_MovementDate",
                table: "ContainerMovements",
                columns: new[] { "CompanyId", "BusinessPartnerId", "ContainerId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_CompanyId_ContainerId",
                table: "ContainerMovements",
                columns: new[] { "CompanyId", "ContainerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_CompanyId_ContainerStoreId",
                table: "ContainerMovements",
                columns: new[] { "CompanyId", "ContainerStoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_CompanyId_InvoiceId_ContainerId",
                table: "ContainerMovements",
                columns: new[] { "CompanyId", "InvoiceId", "ContainerId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DriverTrips_CompanyId_BusinessPartnerId",
                table: "DriverTrips",
                columns: new[] { "CompanyId", "BusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverTrips_CompanyId_DriverId_TripDate_Id",
                table: "DriverTrips",
                columns: new[] { "CompanyId", "DriverId", "TripDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverTrips_CompanyId_InvoiceId",
                table: "DriverTrips",
                columns: new[] { "CompanyId", "InvoiceId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovements_CompanyId_ItemId",
                table: "ItemMovements",
                columns: new[] { "CompanyId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovements_CompanyId_ItemUnitId",
                table: "ItemMovements",
                columns: new[] { "CompanyId", "ItemUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovements_CompanyId_MovementType_ReferenceId_ItemId",
                table: "ItemMovements",
                columns: new[] { "CompanyId", "MovementType", "ReferenceId", "ItemId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovements_CompanyId_StoreId_ItemId_MovementDate_Id",
                table: "ItemMovements",
                columns: new[] { "CompanyId", "StoreId", "ItemId", "MovementDate", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessPartnerMovements");

            migrationBuilder.DropTable(
                name: "ContainerMovements");

            migrationBuilder.DropTable(
                name: "DriverTrips");

            migrationBuilder.DropTable(
                name: "ItemMovements");
        }
    }
}
