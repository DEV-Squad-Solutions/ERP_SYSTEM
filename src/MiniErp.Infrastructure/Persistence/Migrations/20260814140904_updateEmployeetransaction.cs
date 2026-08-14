using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class updateEmployeetransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeTransactions_CompanyId_EmployeeId_Type_IsProcessed",
                table: "EmployeeTransactions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTransactions_CompanyId_PayrollEntryId",
                table: "EmployeeTransactions");

            migrationBuilder.DropColumn(
                name: "IsProcessed",
                table: "EmployeeTransactions");

            migrationBuilder.RenameColumn(
                name: "PayrollEntryId",
                table: "EmployeeTransactions",
                newName: "SourceId");

            migrationBuilder.AddColumn<int>(
                name: "CashVoucherId",
                table: "EmployeeTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RunningBalance",
                table: "EmployeeTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "EmployeeTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_CashVoucherId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "CashVoucherId" },
                filter: "[CashVoucherId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_EmployeeId_Type",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "EmployeeId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_SourceType_SourceId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "SourceType", "SourceId" },
                filter: "[SourceId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeTransactions_CashVouchers_CompanyId_CashVoucherId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "CashVoucherId" },
                principalTable: "CashVouchers",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTransactions_CashVouchers_CompanyId_CashVoucherId",
                table: "EmployeeTransactions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTransactions_CompanyId_CashVoucherId",
                table: "EmployeeTransactions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTransactions_CompanyId_EmployeeId_Type",
                table: "EmployeeTransactions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTransactions_CompanyId_SourceType_SourceId",
                table: "EmployeeTransactions");

            migrationBuilder.DropColumn(
                name: "CashVoucherId",
                table: "EmployeeTransactions");

            migrationBuilder.DropColumn(
                name: "RunningBalance",
                table: "EmployeeTransactions");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "EmployeeTransactions");

            migrationBuilder.RenameColumn(
                name: "SourceId",
                table: "EmployeeTransactions",
                newName: "PayrollEntryId");

            migrationBuilder.AddColumn<bool>(
                name: "IsProcessed",
                table: "EmployeeTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_EmployeeId_Type_IsProcessed",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "EmployeeId", "Type", "IsProcessed" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_PayrollEntryId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "PayrollEntryId" },
                filter: "[PayrollEntryId] IS NOT NULL AND [IsDeleted] = 0");
        }
    }
}
