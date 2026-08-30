using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class modifypayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries",
                sql: "([Overtimebydayunit] IS NULL OR [Overtimebydayunit] >= 0) AND ([Deductionbydayunit] IS NULL OR [Deductionbydayunit] >= 0) AND ([RequiredWorkingDays] IS NULL OR [RequiredWorkingDays] >= 0) AND ([Bonus] IS NULL OR [Bonus] >= 0) AND ([Deduction] IS NULL OR [Deduction] >= 0) AND ([SalaryPerDay] IS NULL OR [SalaryPerDay] >= 0) AND [CalculatedSalary] >= 0 AND [GrossSalary] >= 0 ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollEntries_Amounts_NonNegative",
                table: "PayrollEntries",
                sql: "([Overtimebydayunit] IS NULL OR [Overtimebydayunit] >= 0) AND ([Deductionbydayunit] IS NULL OR [Deductionbydayunit] >= 0) AND ([RequiredWorkingDays] IS NULL OR [RequiredWorkingDays] >= 0) AND ([Bonus] IS NULL OR [Bonus] >= 0) AND ([Deduction] IS NULL OR [Deduction] >= 0) AND ([SalaryPerDay] IS NULL OR [SalaryPerDay] >= 0) AND [CalculatedSalary] >= 0 AND [GrossSalary] >= 0 AND [NetSalary] >= 0");
        }
    }
}
