using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollAttendanceTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add columns to Employees table
            migrationBuilder.AddColumn<int>(
                name: "RequiredWorkingDaysPerMonth",
                table: "Employees",
                type: "int",
                nullable: true,
                defaultValue: 26);

            migrationBuilder.AddColumn<bool>(
                name: "IsSalaryEnabled",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Add columns to PayrollPeriods table
            migrationBuilder.AddColumn<int>(
                name: "WorkingDaysInPeriod",
                table: "PayrollPeriods",
                type: "int",
                nullable: false,
                defaultValue: 26);

            // Add columns to PayrollEntries table
            migrationBuilder.AddColumn<int>(
                name: "PresentDays",
                table: "PayrollEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequiredWorkingDays",
                table: "PayrollEntries",
                type: "int",
                nullable: false,
                defaultValue: 26);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryPerDay",
                table: "PayrollEntries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedSalary",
                table: "PayrollEntries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsSalaryEnabled",
                table: "PayrollEntries",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Add TotalNetSalary to PayrollPeriods if it doesn't exist
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalNetSalary",
                table: "PayrollPeriods",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove columns from Employees table
            migrationBuilder.DropColumn(
                name: "RequiredWorkingDaysPerMonth",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsSalaryEnabled",
                table: "Employees");

            // Remove columns from PayrollPeriods table
            migrationBuilder.DropColumn(
                name: "WorkingDaysInPeriod",
                table: "PayrollPeriods");

            // Remove columns from PayrollEntries table
            migrationBuilder.DropColumn(
                name: "PresentDays",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "RequiredWorkingDays",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "SalaryPerDay",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "CalculatedSalary",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "IsSalaryEnabled",
                table: "PayrollEntries");
        }
    }
}
