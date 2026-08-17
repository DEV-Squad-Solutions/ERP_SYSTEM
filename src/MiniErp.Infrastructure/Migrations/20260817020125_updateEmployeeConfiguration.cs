using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updateEmployeeConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_SalaryType",
                table: "Employees");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_SalaryType",
                table: "Employees",
                sql: "([Type] = 0 AND [DailySalary] IS NOT NULL AND [MonthlySalary] IS NULL) OR ([Type] = 1 AND [MonthlySalary] IS NOT NULL AND [DailySalary] IS NULL)");
        }
    }
}
