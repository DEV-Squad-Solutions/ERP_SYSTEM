using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class modifyEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "AK_PayrollEntries_CompanyId_Id",
                table: "PayrollEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries");

            migrationBuilder.AlterColumn<decimal>(
                name: "NetSalary",
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
                name: "GrossSalary",
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
                name: "Deduction",
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
                name: "Bonus",
                table: "PayrollEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "CashboxId",
                table: "PayrollEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CompanyId_CashboxId",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "CashboxId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CompanyId_EmployeeId",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "EmployeeId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries",
                sql: "([Overtimebydayunit] IS NULL OR [Overtimebydayunit] >= 0) AND ([Deductionbydayunit] IS NULL OR [Deductionbydayunit] >= 0) AND ([RequiredWorkingDays] IS NULL OR [RequiredWorkingDays] >= 0) AND ([Bonus] IS NULL OR [Bonus] >= 0) AND ([Deduction] IS NULL OR [Deduction] >= 0) AND ([SalaryPerDay] IS NULL OR [SalaryPerDay] >= 0) AND [CalculatedSalary] >= 0 AND [GrossSalary] >= 0 AND [NetSalary] >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollEntries_Cashboxes_CompanyId_CashboxId",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "CashboxId" },
                principalTable: "Cashboxes",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollEntries_Cashboxes_CompanyId_CashboxId",
                table: "PayrollEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollEntries_CompanyId_CashboxId",
                table: "PayrollEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollEntries_CompanyId_EmployeeId",
                table: "PayrollEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "CashboxId",
                table: "PayrollEntries");

            migrationBuilder.AlterColumn<decimal>(
                name: "NetSalary",
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
                name: "GrossSalary",
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
                name: "Deduction",
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
                name: "Bonus",
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

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PayrollEntries_CompanyId_Id",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries",
                sql: "([Overtimebydayunit] IS NULL OR [Overtimebydayunit] >= 0) AND ([RequiredWorkingDays] IS NULL OR [RequiredWorkingDays] >= 0) AND [Bonus] >= 0 AND [Deduction] >= 0 AND ([GrossSalary] IS NULL OR [GrossSalary] >= 0) AND ([NetSalary] IS NULL OR [NetSalary] >= 0) AND ([Deductionbydayunit] IS NULL OR [Deductionbydayunit] >= 0)");
        }
    }
}
