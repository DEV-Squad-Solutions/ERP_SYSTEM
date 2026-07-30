using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addexchangerate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_CompanyId_InvoiceId",
                table: "CashVouchers");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseAmount",
                table: "PartnerOpeningBalances",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "PartnerOpeningBalances",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeRateId",
                table: "PartnerOpeningBalances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseDiscountAmount",
                table: "Invoices",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BasePaidAmountAtInvoiceRate",
                table: "Invoices",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseSubtotal",
                table: "Invoices",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseTotal",
                table: "Invoices",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Invoices",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeRateId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseTotal",
                table: "InvoiceLines",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseUnitPrice",
                table: "InvoiceLines",
                type: "decimal(24,8)",
                precision: 24,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BaseCurrency",
                table: "CompanySettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseAmount",
                table: "CashVouchers",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "CashVouchers",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<int>(
                name: "ExchangeRateId",
                table: "CashVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseOpeningBalance",
                table: "Cashboxes",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OpeningBalanceDate",
                table: "Cashboxes",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningExchangeRate",
                table: "Cashboxes",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<int>(
                name: "OpeningExchangeRateId",
                table: "Cashboxes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseCredit",
                table: "BusinessPartnerMovements",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseDebit",
                table: "BusinessPartnerMovements",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "BusinessPartnerMovements",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                    table.UniqueConstraint("AK_ExchangeRates_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_ExchangeRates_Rate_Positive", "[Rate] > 0");
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoicePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    CashVoucherId = table.Column<int>(type: "int", nullable: false),
                    InvoiceCurrency = table.Column<int>(type: "int", nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashboxCurrency = table.Column<int>(type: "int", nullable: false),
                    CashboxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InvoiceToBaseRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    CashboxToBaseRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    AppliedBaseAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    CashboxBaseAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    RealizedExchangeDifference = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
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
                    table.PrimaryKey("PK_InvoicePayments", x => x.Id);
                    table.UniqueConstraint("AK_InvoicePayments_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_InvoicePayments_AppliedAmount_Positive", "[AppliedAmount] > 0");
                    table.CheckConstraint("CK_InvoicePayments_CashboxAmount_Positive", "[CashboxAmount] > 0");
                    table.CheckConstraint("CK_InvoicePayments_Rates_Positive", "[InvoiceToBaseRate] > 0 AND [CashboxToBaseRate] > 0");
                    table.ForeignKey(
                        name: "FK_InvoicePayments_CashVouchers_CompanyId_CashVoucherId",
                        columns: x => new { x.CompanyId, x.CashVoucherId },
                        principalTable: "CashVouchers",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoicePayments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoicePayments_Invoices_CompanyId_InvoiceId",
                        columns: x => new { x.CompanyId, x.InvoiceId },
                        principalTable: "Invoices",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerOpeningBalances_CompanyId_ExchangeRateId",
                table: "PartnerOpeningBalances",
                columns: new[] { "CompanyId", "ExchangeRateId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_ExchangeRateId",
                table: "Invoices",
                columns: new[] { "CompanyId", "ExchangeRateId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_ExchangeRateId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "ExchangeRateId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_InvoiceId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "InvoiceId" },
                filter: "[InvoiceId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Cashboxes_CompanyId_OpeningExchangeRateId",
                table: "Cashboxes",
                columns: new[] { "CompanyId", "OpeningExchangeRateId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CompanyId_Currency_RateDate",
                table: "ExchangeRates",
                columns: new[] { "CompanyId", "Currency", "RateDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_CompanyId_Currency_RateDate_Id",
                table: "ExchangeRates",
                columns: new[] { "CompanyId", "Currency", "RateDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_CompanyId_CashVoucherId",
                table: "InvoicePayments",
                columns: new[] { "CompanyId", "CashVoucherId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_CompanyId_InvoiceId_Id",
                table: "InvoicePayments",
                columns: new[] { "CompanyId", "InvoiceId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Cashboxes_ExchangeRates_CompanyId_OpeningExchangeRateId",
                table: "Cashboxes",
                columns: new[] { "CompanyId", "OpeningExchangeRateId" },
                principalTable: "ExchangeRates",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashVouchers_ExchangeRates_CompanyId_ExchangeRateId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "ExchangeRateId" },
                principalTable: "ExchangeRates",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_ExchangeRates_CompanyId_ExchangeRateId",
                table: "Invoices",
                columns: new[] { "CompanyId", "ExchangeRateId" },
                principalTable: "ExchangeRates",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerOpeningBalances_ExchangeRates_CompanyId_ExchangeRateId",
                table: "PartnerOpeningBalances",
                columns: new[] { "CompanyId", "ExchangeRateId" },
                principalTable: "ExchangeRates",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cashboxes_ExchangeRates_CompanyId_OpeningExchangeRateId",
                table: "Cashboxes");

            migrationBuilder.DropForeignKey(
                name: "FK_CashVouchers_ExchangeRates_CompanyId_ExchangeRateId",
                table: "CashVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_ExchangeRates_CompanyId_ExchangeRateId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerOpeningBalances_ExchangeRates_CompanyId_ExchangeRateId",
                table: "PartnerOpeningBalances");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "InvoicePayments");

            migrationBuilder.DropIndex(
                name: "IX_PartnerOpeningBalances_CompanyId_ExchangeRateId",
                table: "PartnerOpeningBalances");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_ExchangeRateId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_CompanyId_ExchangeRateId",
                table: "CashVouchers");

            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_CompanyId_InvoiceId",
                table: "CashVouchers");

            migrationBuilder.DropIndex(
                name: "IX_Cashboxes_CompanyId_OpeningExchangeRateId",
                table: "Cashboxes");

            migrationBuilder.DropColumn(
                name: "BaseAmount",
                table: "PartnerOpeningBalances");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "PartnerOpeningBalances");

            migrationBuilder.DropColumn(
                name: "ExchangeRateId",
                table: "PartnerOpeningBalances");

            migrationBuilder.DropColumn(
                name: "BaseDiscountAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BasePaidAmountAtInvoiceRate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BaseSubtotal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BaseTotal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ExchangeRateId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BaseTotal",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "BaseUnitPrice",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "BaseCurrency",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "BaseAmount",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "ExchangeRateId",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "BaseOpeningBalance",
                table: "Cashboxes");

            migrationBuilder.DropColumn(
                name: "OpeningBalanceDate",
                table: "Cashboxes");

            migrationBuilder.DropColumn(
                name: "OpeningExchangeRate",
                table: "Cashboxes");

            migrationBuilder.DropColumn(
                name: "OpeningExchangeRateId",
                table: "Cashboxes");

            migrationBuilder.DropColumn(
                name: "BaseCredit",
                table: "BusinessPartnerMovements");

            migrationBuilder.DropColumn(
                name: "BaseDebit",
                table: "BusinessPartnerMovements");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "BusinessPartnerMovements");

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_InvoiceId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "InvoiceId" },
                unique: true,
                filter: "[InvoiceId] IS NOT NULL AND [IsDeleted] = 0");
        }
    }
}
