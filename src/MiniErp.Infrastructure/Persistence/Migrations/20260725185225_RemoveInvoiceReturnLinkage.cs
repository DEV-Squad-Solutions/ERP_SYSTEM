using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInvoiceReturnLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLines_InvoiceLines_CompanyId_OriginalInvoiceLineId",
                table: "InvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Invoices_CompanyId_OriginalInvoiceId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_OriginalInvoiceId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_CompanyId_OriginalInvoiceLineId",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceLineId",
                table: "InvoiceLines");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalInvoiceId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalInvoiceLineId",
                table: "InvoiceLines",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_OriginalInvoiceId",
                table: "Invoices",
                columns: new[] { "CompanyId", "OriginalInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_CompanyId_OriginalInvoiceLineId",
                table: "InvoiceLines",
                columns: new[] { "CompanyId", "OriginalInvoiceLineId" });

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLines_InvoiceLines_CompanyId_OriginalInvoiceLineId",
                table: "InvoiceLines",
                columns: new[] { "CompanyId", "OriginalInvoiceLineId" },
                principalTable: "InvoiceLines",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Invoices_CompanyId_OriginalInvoiceId",
                table: "Invoices",
                columns: new[] { "CompanyId", "OriginalInvoiceId" },
                principalTable: "Invoices",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
