using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addfreetextitemname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_CompanyId_InvoiceId_ItemId",
                table: "InvoiceLines");

            migrationBuilder.AlterColumn<int>(
                name: "ItemUnitId",
                table: "InvoiceLines",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "InvoiceLines",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ItemName",
                table: "InvoiceLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_CompanyId_InvoiceId_ItemId",
                table: "InvoiceLines",
                columns: new[] { "CompanyId", "InvoiceId", "ItemId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ItemId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_CompanyId_InvoiceId_ItemId",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "ItemName",
                table: "InvoiceLines");

            migrationBuilder.AlterColumn<int>(
                name: "ItemUnitId",
                table: "InvoiceLines",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "InvoiceLines",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_CompanyId_InvoiceId_ItemId",
                table: "InvoiceLines",
                columns: new[] { "CompanyId", "InvoiceId", "ItemId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
