using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCrossCurrencyPartnerVoucherSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PartnerAmount",
                table: "CashVouchers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartnerBaseAmount",
                table: "CashVouchers",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartnerCurrency",
                table: "CashVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartnerExchangeRate",
                table: "CashVouchers",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartnerExchangeRateId",
                table: "CashVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_PartnerExchangeRateId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "PartnerExchangeRateId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CashVouchers_ExchangeRates_CompanyId_PartnerExchangeRateId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "PartnerExchangeRateId" },
                principalTable: "ExchangeRates",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashVouchers_ExchangeRates_CompanyId_PartnerExchangeRateId",
                table: "CashVouchers");

            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_CompanyId_PartnerExchangeRateId",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "PartnerAmount",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "PartnerBaseAmount",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "PartnerCurrency",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "PartnerExchangeRate",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "PartnerExchangeRateId",
                table: "CashVouchers");
        }
    }
}
