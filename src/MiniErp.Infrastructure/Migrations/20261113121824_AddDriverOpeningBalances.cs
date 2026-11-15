using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverOpeningBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriverOpeningBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FiscalYearId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    BalanceType = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_DriverOpeningBalances", x => x.Id);
                    table.CheckConstraint("CK_DriverOpeningBalances_Amount_Positive", "[Amount] > 0");
                    table.CheckConstraint("CK_DriverOpeningBalances_BalanceType", "[BalanceType] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_DriverOpeningBalances_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverOpeningBalances_Drivers_CompanyId_DriverId",
                        columns: x => new { x.CompanyId, x.DriverId },
                        principalTable: "Drivers",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverOpeningBalances_FiscalYears_CompanyId_FiscalYearId",
                        columns: x => new { x.CompanyId, x.FiscalYearId },
                        principalTable: "FiscalYears",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverOpeningBalances_Company_FiscalYear",
                table: "DriverOpeningBalances",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverOpeningBalances_CompanyId_DocumentNumber",
                table: "DriverOpeningBalances",
                columns: new[] { "CompanyId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverOpeningBalances_CompanyId_DriverId",
                table: "DriverOpeningBalances",
                columns: new[] { "CompanyId", "DriverId" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverOpeningBalances_CompanyId_FiscalYearId_DriverId",
                table: "DriverOpeningBalances",
                columns: new[] { "CompanyId", "FiscalYearId", "DriverId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverOpeningBalances");
        }
    }
}
