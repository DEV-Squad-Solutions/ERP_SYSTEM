using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeDriverAndBusinessPartnerNamesUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [Drivers]
                    WHERE [IsDeleted] = 0
                    GROUP BY [CompanyId], LTRIM(RTRIM([Name]))
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Cannot enforce unique driver names because active duplicates exist within a company.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [BusinessPartners]
                    WHERE [IsDeleted] = 0
                    GROUP BY [CompanyId], LTRIM(RTRIM([Name]))
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Cannot enforce unique business partner names because active duplicates exist within a company.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Drivers_CompanyId_Name",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_CompanyId_Name",
                table: "BusinessPartners");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CompanyId_Name",
                table: "Drivers",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_CompanyId_Name",
                table: "BusinessPartners",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drivers_CompanyId_Name",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_CompanyId_Name",
                table: "BusinessPartners");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CompanyId_Name",
                table: "Drivers",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_CompanyId_Name",
                table: "BusinessPartners",
                columns: new[] { "CompanyId", "Name" });
        }
    }
}
