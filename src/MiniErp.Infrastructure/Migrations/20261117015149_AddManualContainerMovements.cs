using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualContainerMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContainerMovements_CompanyId_InvoiceId_ContainerId",
                table: "ContainerMovements");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "ContainerMovements",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "ContainerMovements",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ContainerMovements",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "ContainerMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // كل الحركات الموجودة قبل الترحيل أُنشئت من فواتير.
            migrationBuilder.Sql(
                "UPDATE [ContainerMovements] SET [SourceType] = 1 " +
                "WHERE [SourceType] = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_CompanyId_InvoiceId_ContainerId",
                table: "ContainerMovements",
                columns: new[] { "CompanyId", "InvoiceId", "ContainerId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [InvoiceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContainerMovements_CompanyId_InvoiceId_ContainerId",
                table: "ContainerMovements");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ContainerMovements");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "ContainerMovements");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "ContainerMovements",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "ContainerMovements",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_CompanyId_InvoiceId_ContainerId",
                table: "ContainerMovements",
                columns: new[] { "CompanyId", "InvoiceId", "ContainerId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
