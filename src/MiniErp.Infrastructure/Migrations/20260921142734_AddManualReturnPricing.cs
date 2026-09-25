using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualReturnPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReturnPriceDifferenceReason",
                table: "InvoiceLines",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnPriceMode",
                table: "InvoiceLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SourceUnitPriceSnapshot",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Legacy linked returns always followed the source commercial
            // price. Preserve that behavior explicitly while leaving
            // unlinked and non-return lines nullable.
            migrationBuilder.Sql(
                """
                UPDATE returnLine
                SET ReturnPriceMode = 1,
                    SourceUnitPriceSnapshot = sourceLine.Price
                FROM InvoiceLines AS returnLine
                INNER JOIN Invoices AS returnInvoice
                    ON returnInvoice.CompanyId = returnLine.CompanyId
                   AND returnInvoice.Id = returnLine.InvoiceId
                INNER JOIN InvoiceLines AS sourceLine
                    ON sourceLine.CompanyId = returnLine.CompanyId
                   AND sourceLine.Id = returnLine.SourceInvoiceLineId
                WHERE returnInvoice.InvoiceType IN (3, 4)
                  AND returnLine.SourceInvoiceLineId IS NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceLines_ReturnPriceMode_Valid",
                table: "InvoiceLines",
                sql: "[ReturnPriceMode] IS NULL OR [ReturnPriceMode] IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceLines_ReturnPriceMode_Valid",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "ReturnPriceDifferenceReason",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "ReturnPriceMode",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "SourceUnitPriceSnapshot",
                table: "InvoiceLines");
        }
    }
}
