using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class additemCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ItemsCategoryId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ItemsCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeletedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemsCategories", x => x.Id);
                    table.UniqueConstraint("AK_ItemsCategories_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_ItemsCategories_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_ItemsCategoryId",
                table: "Invoices",
                columns: new[] { "CompanyId", "ItemsCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemsCategories_CompanyId_IsActive_Name_Id",
                table: "ItemsCategories",
                columns: new[] { "CompanyId", "IsActive", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemsCategories_CompanyId_Name",
                table: "ItemsCategories",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_ItemsCategories_CompanyId_ItemsCategoryId",
                table: "Invoices",
                columns: new[] { "CompanyId", "ItemsCategoryId" },
                principalTable: "ItemsCategories",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_ItemsCategories_CompanyId_ItemsCategoryId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "ItemsCategories");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_ItemsCategoryId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ItemsCategoryId",
                table: "Invoices");
        }
    }
}
