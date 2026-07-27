using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerMovements_CompanyId_InvoiceId",
                table: "BusinessPartnerMovements");

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "DriverTrips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostNotes",
                table: "DriverTrips",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DriverTrips",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "BusinessPartnerMovements",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "CashVoucherId",
                table: "BusinessPartnerMovements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cashboxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Cashboxes", x => x.Id);
                    table.UniqueConstraint("AK_Cashboxes_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_Cashboxes_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashMovementTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    PartnerEffect = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CashMovementTypes", x => x.Id);
                    table.UniqueConstraint("AK_CashMovementTypes_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_CashMovementTypes_Direction", "[Direction] IN (1, 2)");
                    table.CheckConstraint("CK_CashMovementTypes_PartnerEffect", "[PartnerEffect] IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_CashMovementTypes_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    VoucherNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VoucherDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    CashboxId = table.Column<int>(type: "int", nullable: false),
                    CashMovementTypeId = table.Column<int>(type: "int", nullable: false),
                    PartyType = table.Column<int>(type: "int", nullable: false),
                    BusinessPartnerId = table.Column<int>(type: "int", nullable: true),
                    DriverId = table.Column<int>(type: "int", nullable: true),
                    DriverTripId = table.Column<int>(type: "int", nullable: true),
                    ExternalPartyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CashVouchers", x => x.Id);
                    table.UniqueConstraint("AK_CashVouchers_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_CashVouchers_Amount_Positive", "[Amount] > 0");
                    table.CheckConstraint("CK_CashVouchers_Direction", "[Direction] IN (1, 2)");
                    table.CheckConstraint("CK_CashVouchers_PartyShape", "([PartyType] = 1 AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL) OR ([PartyType] = 2 AND [BusinessPartnerId] IS NOT NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL) OR ([PartyType] = 3 AND [BusinessPartnerId] IS NULL AND [DriverId] IS NOT NULL AND [ExternalPartyName] IS NULL) OR ([PartyType] = 4 AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NOT NULL)");
                    table.CheckConstraint("CK_CashVouchers_PartyType", "[PartyType] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_CashVouchers_BusinessPartners_CompanyId_BusinessPartnerId",
                        columns: x => new { x.CompanyId, x.BusinessPartnerId },
                        principalTable: "BusinessPartners",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashVouchers_CashMovementTypes_CompanyId_CashMovementTypeId",
                        columns: x => new { x.CompanyId, x.CashMovementTypeId },
                        principalTable: "CashMovementTypes",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashVouchers_Cashboxes_CompanyId_CashboxId",
                        columns: x => new { x.CompanyId, x.CashboxId },
                        principalTable: "Cashboxes",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashVouchers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashVouchers_DriverTrips_CompanyId_DriverTripId",
                        columns: x => new { x.CompanyId, x.DriverTripId },
                        principalTable: "DriverTrips",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashVouchers_Drivers_CompanyId_DriverId",
                        columns: x => new { x.CompanyId, x.DriverId },
                        principalTable: "Drivers",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerMovements_CompanyId_CashVoucherId",
                table: "BusinessPartnerMovements",
                columns: new[] { "CompanyId", "CashVoucherId" },
                unique: true,
                filter: "[CashVoucherId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerMovements_CompanyId_InvoiceId",
                table: "BusinessPartnerMovements",
                columns: new[] { "CompanyId", "InvoiceId" },
                unique: true,
                filter: "[InvoiceId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusinessPartnerMovements_ExactlyOneSource",
                table: "BusinessPartnerMovements",
                sql: "([InvoiceId] IS NOT NULL AND [CashVoucherId] IS NULL) OR ([InvoiceId] IS NULL AND [CashVoucherId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Cashboxes_CompanyId_Code",
                table: "Cashboxes",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Cashboxes_CompanyId_IsActive_Name_Id",
                table: "Cashboxes",
                columns: new[] { "CompanyId", "IsActive", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Cashboxes_CompanyId_Name",
                table: "Cashboxes",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementTypes_CompanyId_Direction_IsActive_Name_Id",
                table: "CashMovementTypes",
                columns: new[] { "CompanyId", "Direction", "IsActive", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementTypes_CompanyId_Direction_Name",
                table: "CashMovementTypes",
                columns: new[] { "CompanyId", "Direction", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_BusinessPartnerId_VoucherDate_Id",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "BusinessPartnerId", "VoucherDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_CashboxId_VoucherDate_Id",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "CashboxId", "VoucherDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_CashMovementTypeId_VoucherDate_Id",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "CashMovementTypeId", "VoucherDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_DriverId_VoucherDate_Id",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "DriverId", "VoucherDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_DriverTripId_VoucherDate_Id",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "DriverTripId", "VoucherDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_VoucherNumber",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "VoucherNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartnerMovements_CashVouchers_CompanyId_CashVoucherId",
                table: "BusinessPartnerMovements",
                columns: new[] { "CompanyId", "CashVoucherId" },
                principalTable: "CashVouchers",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartnerMovements_CashVouchers_CompanyId_CashVoucherId",
                table: "BusinessPartnerMovements");

            migrationBuilder.Sql(
                """
                DELETE FROM [BusinessPartnerMovements]
                WHERE [CashVoucherId] IS NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "CashVouchers");

            migrationBuilder.DropTable(
                name: "CashMovementTypes");

            migrationBuilder.DropTable(
                name: "Cashboxes");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerMovements_CompanyId_CashVoucherId",
                table: "BusinessPartnerMovements");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerMovements_CompanyId_InvoiceId",
                table: "BusinessPartnerMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusinessPartnerMovements_ExactlyOneSource",
                table: "BusinessPartnerMovements");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "DriverTrips");

            migrationBuilder.DropColumn(
                name: "CostNotes",
                table: "DriverTrips");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DriverTrips");

            migrationBuilder.DropColumn(
                name: "CashVoucherId",
                table: "BusinessPartnerMovements");

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "BusinessPartnerMovements",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerMovements_CompanyId_InvoiceId",
                table: "BusinessPartnerMovements",
                columns: new[] { "CompanyId", "InvoiceId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
