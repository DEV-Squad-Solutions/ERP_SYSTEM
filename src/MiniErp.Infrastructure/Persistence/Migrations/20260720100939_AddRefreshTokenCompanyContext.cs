using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenCompanyContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAtUtc",
                table: "RefreshTokens");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "RefreshTokens",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [RefreshTokens]
                SET [RevokedAtUtc] = SYSUTCDATETIME()
                WHERE [CompanyId] IS NULL AND [RevokedAtUtc] IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_CompanyId",
                table: "RefreshTokens",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_CompanyId_ExpiresAtUtc",
                table: "RefreshTokens",
                columns: new[] { "UserId", "CompanyId", "ExpiresAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Companies_CompanyId",
                table: "RefreshTokens",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Companies_CompanyId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_CompanyId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId_CompanyId_ExpiresAtUtc",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "RefreshTokens");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAtUtc",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAtUtc" });
        }
    }
}
