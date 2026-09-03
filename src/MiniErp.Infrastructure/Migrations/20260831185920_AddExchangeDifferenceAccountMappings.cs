using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeDifferenceAccountMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountMappings_MappingType",
                table: "AccountMappings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountMappings_MappingType",
                table: "AccountMappings",
                sql: "[MappingType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountMappings_MappingType",
                table: "AccountMappings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountMappings_MappingType",
                table: "AccountMappings",
                sql: "[MappingType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)");
        }
    }
}
