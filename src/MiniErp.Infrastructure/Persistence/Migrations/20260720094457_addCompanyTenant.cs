using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addCompanyTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_ItemUnits_ItemUnitId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_ItemUnits_Name",
                table: "ItemUnits");

            migrationBuilder.DropIndex(
                name: "IX_Items_Code",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_ItemUnitId",
                table: "Items");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "ItemUnits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Items",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [ItemUnits]) OR EXISTS (SELECT 1 FROM [Items])
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [Companies] WHERE [IsDeleted] = 0)
                    BEGIN
                        INSERT INTO [Companies]
                            ([Name], [Address], [CommercialRegister], [TaxNumber], [ManagerName],
                             [CreatedById], [CreatedOn], [CreatedByPc], [IsDeleted])
                        VALUES
                            (N'مجموعة السلام القابضة', N'شارع النصر، مدينة نصر، القاهرة',
                             N'54321', N'456789123', N'خالد السلام',
                             N'migration', SYSUTCDATETIME(), N'migration', 0);
                    END;

                    DECLARE @CompanyId int =
                    (
                        SELECT TOP (1) [Id]
                        FROM [Companies]
                        WHERE [IsDeleted] = 0
                        ORDER BY
                            CASE
                                WHEN [CommercialRegister] = N'54321' OR [TaxNumber] = N'456789123'
                                    THEN 0
                                ELSE 1
                            END,
                            [Id]
                    );

                    UPDATE [ItemUnits]
                    SET [CompanyId] = @CompanyId
                    WHERE [CompanyId] IS NULL;

                    UPDATE [Items]
                    SET [CompanyId] = @CompanyId
                    WHERE [CompanyId] IS NULL;
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "ItemUnits",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "Items",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ItemUnits_CompanyId_Id",
                table: "ItemUnits",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_Stores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stores_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserCompanies",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompanies", x => new { x.UserId, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_UserCompanies_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCompanies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnits_CompanyId_Name",
                table: "ItemUnits",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CompanyId_Code",
                table: "Items",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CompanyId_ItemUnitId",
                table: "Items",
                columns: new[] { "CompanyId", "ItemUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CompanyId_Code",
                table: "Stores",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Name",
                table: "Stores",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_CompanyId",
                table: "UserCompanies",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Companies_CompanyId",
                table: "Items",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_ItemUnits_CompanyId_ItemUnitId",
                table: "Items",
                columns: new[] { "CompanyId", "ItemUnitId" },
                principalTable: "ItemUnits",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemUnits_Companies_CompanyId",
                table: "ItemUnits",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Companies_CompanyId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_ItemUnits_CompanyId_ItemUnitId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemUnits_Companies_CompanyId",
                table: "ItemUnits");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "UserCompanies");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ItemUnits_CompanyId_Id",
                table: "ItemUnits");

            migrationBuilder.DropIndex(
                name: "IX_ItemUnits_CompanyId_Name",
                table: "ItemUnits");

            migrationBuilder.DropIndex(
                name: "IX_Items_CompanyId_Code",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_CompanyId_ItemUnitId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ItemUnits");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Items");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUnits_Name",
                table: "ItemUnits",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Code",
                table: "Items",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ItemUnitId",
                table: "Items",
                column: "ItemUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_ItemUnits_ItemUnitId",
                table: "Items",
                column: "ItemUnitId",
                principalTable: "ItemUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
