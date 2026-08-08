using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashboxTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CashboxTransferId",
                table: "CashVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashboxTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    TransferNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceCashboxId = table.Column<int>(type: "int", nullable: false),
                    DestinationCashboxId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_CashboxTransfers", x => x.Id);
                    table.UniqueConstraint("AK_CashboxTransfers_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_CashboxTransfers_DifferentCashboxes", "[SourceCashboxId] <> [DestinationCashboxId]");
                    table.ForeignKey(
                        name: "FK_CashboxTransfers_Cashboxes_CompanyId_DestinationCashboxId",
                        columns: x => new { x.CompanyId, x.DestinationCashboxId },
                        principalTable: "Cashboxes",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashboxTransfers_Cashboxes_CompanyId_SourceCashboxId",
                        columns: x => new { x.CompanyId, x.SourceCashboxId },
                        principalTable: "Cashboxes",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashboxTransfers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_CashboxTransferId_Direction",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "CashboxTransferId", "Direction" },
                unique: true,
                filter: "[CashboxTransferId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashVouchers_TransferShape",
                table: "CashVouchers",
                sql: "[CashboxTransferId] IS NULL OR ([CashboxId] IS NOT NULL AND [CashMovementTypeId] IS NULL AND [InvoiceId] IS NULL AND [PartyType] = 1)");

            migrationBuilder.CreateIndex(
                name: "IX_CashboxTransfers_CompanyId_DestinationCashboxId",
                table: "CashboxTransfers",
                columns: new[] { "CompanyId", "DestinationCashboxId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashboxTransfers_CompanyId_SourceCashboxId",
                table: "CashboxTransfers",
                columns: new[] { "CompanyId", "SourceCashboxId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashboxTransfers_CompanyId_TransferDate_Id",
                table: "CashboxTransfers",
                columns: new[] { "CompanyId", "TransferDate", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashboxTransfers_CompanyId_TransferNumber",
                table: "CashboxTransfers",
                columns: new[] { "CompanyId", "TransferNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_CashVouchers_CashboxTransfers_CompanyId_CashboxTransferId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "CashboxTransferId" },
                principalTable: "CashboxTransfers",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashVouchers_CashboxTransfers_CompanyId_CashboxTransferId",
                table: "CashVouchers");

            migrationBuilder.DropTable(
                name: "CashboxTransfers");

            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_CompanyId_CashboxTransferId_Direction",
                table: "CashVouchers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashVouchers_TransferShape",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "CashboxTransferId",
                table: "CashVouchers");
        }
    }
}
