using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameActualDriverIdToActualDriverName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ActualDriverId",
                table: "Invoices",
                newName: "ActualDriverName");

            migrationBuilder.RenameColumn(
                name: "ActualDriverId",
                table: "DriverTrips",
                newName: "ActualDriverName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ActualDriverName",
                table: "Invoices",
                newName: "ActualDriverId");

            migrationBuilder.RenameColumn(
                name: "ActualDriverName",
                table: "DriverTrips",
                newName: "ActualDriverId");
        }
    }
}
