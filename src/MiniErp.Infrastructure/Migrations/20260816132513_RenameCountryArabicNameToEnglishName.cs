using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameCountryArabicNameToEnglishName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ArabicName",
                table: "Countries",
                newName: "EnglishName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EnglishName",
                table: "Countries",
                newName: "ArabicName");
        }
    }
}
