using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueActiveContainerStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT 1
                    FROM [Stores]
                    WHERE [BusinessPartnerId] IS NOT NULL
                      AND [IsContainerStore] = 1
                      AND [IsActive] = 1
                      AND [IsDeleted] = 0
                    GROUP BY [CompanyId], [BusinessPartnerId]
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    THROW 51000, 'Cannot enforce one active container store per business partner because duplicate active records exist.', 1;
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Stores_CompanyId_BusinessPartnerId",
                table: "Stores");

            migrationBuilder.CreateIndex(
                name: "UX_Stores_CompanyId_BusinessPartnerId_ActiveContainer",
                table: "Stores",
                columns: new[] { "CompanyId", "BusinessPartnerId" },
                unique: true,
                filter: "[BusinessPartnerId] IS NOT NULL AND [IsContainerStore] = 1 AND [IsActive] = 1 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Stores_CompanyId_BusinessPartnerId_ActiveContainer",
                table: "Stores");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CompanyId_BusinessPartnerId",
                table: "Stores",
                columns: new[] { "CompanyId", "BusinessPartnerId" },
                filter: "[BusinessPartnerId] IS NOT NULL AND [IsDeleted] = 0");
        }
    }
}
