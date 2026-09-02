using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashVoucherAccountAndAccountMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "CashVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FiscalYearId = table.Column<int>(type: "int", nullable: false),
                    MappingType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AccountMappings", x => x.Id);
                    table.UniqueConstraint("AK_AccountMappings_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_AccountMappings_MappingType", "[MappingType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)");
                    table.CheckConstraint("CK_AccountMappings_SourceShape", "(([MappingType] IN (1, 2) AND [SourceId] IS NOT NULL) OR ([MappingType] NOT IN (1, 2) AND [SourceId] IS NULL))");
                    table.ForeignKey(
                        name: "FK_AccountMappings_Accounts_CompanyId_AccountId",
                        columns: x => new { x.CompanyId, x.AccountId },
                        principalTable: "Accounts",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountMappings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountMappings_FiscalYears_CompanyId_FiscalYearId",
                        columns: x => new { x.CompanyId, x.FiscalYearId },
                        principalTable: "FiscalYears",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_AccountId_VoucherDate_Id",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "AccountId", "VoucherDate", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashVouchers_AccountTargetShape",
                table: "CashVouchers",
                sql: "[AccountId] IS NULL OR ([PartyType] = 1 AND [InvoiceId] IS NULL AND [CashboxTransferId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_CompanyId_AccountId",
                table: "AccountMappings",
                columns: new[] { "CompanyId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_Scope_Account",
                table: "AccountMappings",
                columns: new[] { "CompanyId", "FiscalYearId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "UX_AccountMappings_Scope_Type_Source",
                table: "AccountMappings",
                columns: new[] { "CompanyId", "FiscalYearId", "MappingType", "SourceId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_CashVouchers_Accounts_CompanyId_AccountId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "AccountId" },
                principalTable: "Accounts",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashVouchers_Accounts_CompanyId_AccountId",
                table: "CashVouchers");

            migrationBuilder.DropTable(
                name: "AccountMappings");

            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_CompanyId_AccountId_VoucherDate_Id",
                table: "CashVouchers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashVouchers_AccountTargetShape",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "CashVouchers");
        }
    }
}
