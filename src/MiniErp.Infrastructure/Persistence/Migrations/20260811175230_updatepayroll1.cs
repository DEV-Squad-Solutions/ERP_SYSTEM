using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class updatepayroll1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollEntries_CompanyId_IsTakeSalary",
                table: "PayrollEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollEntries_Days",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "WorkedDays",
                table: "PayrollEntries");

            migrationBuilder.RenameColumn(
                name: "TotalDebits",
                table: "PayrollEntries",
                newName: "WorkedDaysbydayunit");

            migrationBuilder.RenameColumn(
                name: "TotalCredits",
                table: "PayrollEntries",
                newName: "Deduction");

            migrationBuilder.AlterColumn<decimal>(
                name: "RequiredWorkingDays",
                table: "PayrollEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Overtimebydayunit",
                table: "PayrollEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "Bonus",
                table: "PayrollEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CashVoucherId",
                table: "PayrollEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Deductionbydayunit",
                table: "PayrollEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CompanyId_CashVoucherId",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "CashVoucherId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries",
                sql: "([Overtimebydayunit] IS NULL OR [Overtimebydayunit] >= 0) AND ([RequiredWorkingDays] IS NULL OR [RequiredWorkingDays] >= 0) AND [Bonus] >= 0 AND [Deduction] >= 0 AND ([GrossSalary] IS NULL OR [GrossSalary] >= 0) AND ([NetSalary] IS NULL OR [NetSalary] >= 0) AND ([Deductionbydayunit] IS NULL OR [Deductionbydayunit] >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollEntries_Days",
                table: "PayrollEntries",
                sql: "[PresentDays] >= 0 AND [AbsentDays] >= 0 AND [WorkedDaysbydayunit] >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollEntries_CashVouchers_CompanyId_CashVoucherId",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "CashVoucherId" },
                principalTable: "CashVouchers",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollEntries_CashVouchers_CompanyId_CashVoucherId",
                table: "PayrollEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollEntries_CompanyId_CashVoucherId",
                table: "PayrollEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollEntries_Days",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "Bonus",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "CashVoucherId",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "Deductionbydayunit",
                table: "PayrollEntries");

            migrationBuilder.RenameColumn(
                name: "WorkedDaysbydayunit",
                table: "PayrollEntries",
                newName: "TotalDebits");

            migrationBuilder.RenameColumn(
                name: "Deduction",
                table: "PayrollEntries",
                newName: "TotalCredits");

            migrationBuilder.AlterColumn<decimal>(
                name: "RequiredWorkingDays",
                table: "PayrollEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Overtimebydayunit",
                table: "PayrollEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkedDays",
                table: "PayrollEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CompanyId_IsTakeSalary",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "IsTakeSalary" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries",
                sql: "[Overtimebydayunit] >= 0 AND [RequiredWorkingDays] >= 0 AND ([SalaryPerDay] IS NULL OR [SalaryPerDay] >= 0) AND [CalculatedSalary] >= 0 AND [TotalCredits] >= 0 AND [TotalDebits] >= 0 AND ([GrossSalary] IS NULL OR [GrossSalary] >= 0) AND ([NetSalary] IS NULL OR [NetSalary] >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollEntries_Days",
                table: "PayrollEntries",
                sql: "[PresentDays] >= 0 AND [AbsentDays] >= 0 AND [WorkedDays] >= 0");
        }
    }
}
