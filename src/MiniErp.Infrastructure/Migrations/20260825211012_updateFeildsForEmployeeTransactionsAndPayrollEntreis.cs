using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updateFeildsForEmployeeTransactionsAndPayrollEntreis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollEntries_CashVouchers_CompanyId_CashVoucherId",
                table: "PayrollEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollEntries_Cashboxes_CompanyId_CashboxId",
                table: "PayrollEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollEntries_CompanyId_CashboxId",
                table: "PayrollEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollEntries_CompanyId_CashVoucherId",
                table: "PayrollEntries");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTransactions_CompanyId_CashVoucherId",
                table: "EmployeeTransactions");

            migrationBuilder.DropColumn(
                name: "CashVoucherId",
                table: "PayrollEntries");

            migrationBuilder.RenameColumn(
                name: "CashboxId",
                table: "PayrollEntries",
                newName: "EmployeeTransactionId");

            migrationBuilder.AlterColumn<int>(
                name: "CashVoucherId",
                table: "EmployeeTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CashBoxId",
                table: "EmployeeTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_EmployeeTransactionId",
                table: "PayrollEntries",
                column: "EmployeeTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_CashBoxId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "CashBoxId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_CashVoucherId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "CashVoucherId" });

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeTransactions_Cashboxes_CompanyId_CashBoxId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "CashBoxId" },
                principalTable: "Cashboxes",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollEntries_EmployeeTransactions_EmployeeTransactionId",
                table: "PayrollEntries",
                column: "EmployeeTransactionId",
                principalTable: "EmployeeTransactions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTransactions_Cashboxes_CompanyId_CashBoxId",
                table: "EmployeeTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollEntries_EmployeeTransactions_EmployeeTransactionId",
                table: "PayrollEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollEntries_EmployeeTransactionId",
                table: "PayrollEntries");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTransactions_CompanyId_CashBoxId",
                table: "EmployeeTransactions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTransactions_CompanyId_CashVoucherId",
                table: "EmployeeTransactions");

            migrationBuilder.DropColumn(
                name: "CashBoxId",
                table: "EmployeeTransactions");

            migrationBuilder.RenameColumn(
                name: "EmployeeTransactionId",
                table: "PayrollEntries",
                newName: "CashboxId");

            migrationBuilder.AddColumn<int>(
                name: "CashVoucherId",
                table: "PayrollEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CashVoucherId",
                table: "EmployeeTransactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CompanyId_CashboxId",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "CashboxId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CompanyId_CashVoucherId",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "CashVoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_CashVoucherId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "CashVoucherId" },
                filter: "[CashVoucherId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollEntries_CashVouchers_CompanyId_CashVoucherId",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "CashVoucherId" },
                principalTable: "CashVouchers",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollEntries_Cashboxes_CompanyId_CashboxId",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "CashboxId" },
                principalTable: "Cashboxes",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
