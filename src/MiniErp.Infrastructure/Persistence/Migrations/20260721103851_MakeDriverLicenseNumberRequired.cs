using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeDriverLicenseNumberRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drivers_CompanyId_LicenseNumber",
                table: "Drivers");

            migrationBuilder.Sql(
                """
                UPDATE [Drivers]
                SET [LicenseNumber] = CONCAT(N'MISSING-LICENCE-', [CompanyId], N'-', [Id])
                WHERE [LicenseNumber] IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "LicenseNumber",
                table: "Drivers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CompanyId_LicenseNumber",
                table: "Drivers",
                columns: new[] { "CompanyId", "LicenseNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drivers_CompanyId_LicenseNumber",
                table: "Drivers");

            migrationBuilder.AlterColumn<string>(
                name: "LicenseNumber",
                table: "Drivers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CompanyId_LicenseNumber",
                table: "Drivers",
                columns: new[] { "CompanyId", "LicenseNumber" },
                unique: true,
                filter: "[LicenseNumber] IS NOT NULL AND [IsDeleted] = 0");
        }
    }
}
