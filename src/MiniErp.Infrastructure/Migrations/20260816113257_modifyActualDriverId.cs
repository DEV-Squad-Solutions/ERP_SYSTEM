using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class modifyActualDriverId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DriverTrips_Drivers_CompanyId_ActualDriverId",
                table: "DriverTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Drivers_CompanyId_ActualDriverId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_ActualDriverId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_DriverTrips_CompanyId_ActualDriverId",
                table: "DriverTrips");

            migrationBuilder.AlterColumn<string>(
                name: "ActualDriverId",
                table: "Invoices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActualDriverId",
                table: "DriverTrips",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ActualDriverId",
                table: "Invoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ActualDriverId",
                table: "DriverTrips",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_ActualDriverId",
                table: "Invoices",
                columns: new[] { "CompanyId", "ActualDriverId" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverTrips_CompanyId_ActualDriverId",
                table: "DriverTrips",
                columns: new[] { "CompanyId", "ActualDriverId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DriverTrips_Drivers_CompanyId_ActualDriverId",
                table: "DriverTrips",
                columns: new[] { "CompanyId", "ActualDriverId" },
                principalTable: "Drivers",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Drivers_CompanyId_ActualDriverId",
                table: "Invoices",
                columns: new[] { "CompanyId", "ActualDriverId" },
                principalTable: "Drivers",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
