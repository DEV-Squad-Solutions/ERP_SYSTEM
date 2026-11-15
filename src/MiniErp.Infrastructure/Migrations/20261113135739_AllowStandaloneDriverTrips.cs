using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowStandaloneDriverTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverTrips_CompanyId_InvoiceId",
                table: "DriverTrips");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "DriverTrips",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "DriverTrips",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "BusinessPartnerId",
                table: "DriverTrips",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_DriverTrips_CompanyId_InvoiceId",
                table: "DriverTrips",
                columns: new[] { "CompanyId", "InvoiceId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [InvoiceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1 FROM [DriverTrips] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [InvoiceId] IS NULL OR [InvoiceNumber] IS NULL OR [BusinessPartnerId] IS NULL
                )
                    THROW 51000, 'Cannot roll back standalone driver trips while rows with nullable invoice or partner data exist.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_DriverTrips_CompanyId_InvoiceId",
                table: "DriverTrips");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "DriverTrips",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "DriverTrips",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BusinessPartnerId",
                table: "DriverTrips",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverTrips_CompanyId_InvoiceId",
                table: "DriverTrips",
                columns: new[] { "CompanyId", "InvoiceId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
