using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCashboxPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllCashboxes",
                table: "UserCompanies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CashboxPermissionsRevision",
                table: "UserCompanies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "UserCashboxPermissions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CashboxId = table.Column<int>(type: "int", nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    CanExecute = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCashboxPermissions", x => new { x.UserId, x.CompanyId, x.CashboxId });
                    table.CheckConstraint("CK_UserCashboxPermissions_ExecuteIncludesView", "[CanExecute] = 0 OR [CanView] = 1");
                    table.ForeignKey(
                        name: "FK_UserCashboxPermissions_Cashboxes_CompanyId_CashboxId",
                        columns: x => new { x.CompanyId, x.CashboxId },
                        principalTable: "Cashboxes",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCashboxPermissions_UserCompanies_UserId_CompanyId",
                        columns: x => new { x.UserId, x.CompanyId },
                        principalTable: "UserCompanies",
                        principalColumns: new[] { "UserId", "CompanyId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCashboxPermissions_CompanyId_CashboxId",
                table: "UserCashboxPermissions",
                columns: new[] { "CompanyId", "CashboxId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCashboxPermissions");

            migrationBuilder.DropColumn(
                name: "AllCashboxes",
                table: "UserCompanies");

            migrationBuilder.DropColumn(
                name: "CashboxPermissionsRevision",
                table: "UserCompanies");
        }
    }
}
