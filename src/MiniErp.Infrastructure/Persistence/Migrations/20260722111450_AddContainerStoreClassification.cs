using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContainerStoreClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BusinessPartnerId",
                table: "Stores",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsContainerStore",
                table: "Stores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_BusinessPartners_CompanyId_Id",
                table: "BusinessPartners",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CompanyId_BusinessPartnerId",
                table: "Stores",
                columns: new[] { "CompanyId", "BusinessPartnerId" },
                filter: "[BusinessPartnerId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stores_TypeBusinessPartner",
                table: "Stores",
                sql: "([IsContainerStore] = 0 AND [BusinessPartnerId] IS NULL) OR ([IsContainerStore] = 1 AND [BusinessPartnerId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_BusinessPartners_CompanyId_BusinessPartnerId",
                table: "Stores",
                columns: new[] { "CompanyId", "BusinessPartnerId" },
                principalTable: "BusinessPartners",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stores_BusinessPartners_CompanyId_BusinessPartnerId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Stores_CompanyId_BusinessPartnerId",
                table: "Stores");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stores_TypeBusinessPartner",
                table: "Stores");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_BusinessPartners_CompanyId_Id",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerId",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "IsContainerStore",
                table: "Stores");
        }
    }
}
