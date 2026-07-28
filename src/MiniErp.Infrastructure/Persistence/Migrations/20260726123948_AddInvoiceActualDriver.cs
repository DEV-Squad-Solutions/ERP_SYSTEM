using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceActualDriver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActualDriverId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualDriverId",
                table: "DriverTrips",
                type: "int",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(
                name: "ActualDriverId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ActualDriverId",
                table: "DriverTrips");
        }
    }
}
