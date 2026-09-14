using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFeildsForEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDayRatio",
                table: "EmployeeAttendances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDaysDeductionRatio",
                table: "EmployeeAttendances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_WorkOverTimeRatio",
                table: "EmployeeAttendances");

            migrationBuilder.AddColumn<bool>(
                name: "IsSalaryMovedToEmployeeAccount",
                table: "PayrollEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlaceName",
                table: "Employees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkPlaceStatus",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_WorkPlaceStatus",
                table: "Employees",
                sql: "[WorkPlaceStatus] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDayRatio",
                table: "EmployeeAttendances",
                sql: "[WorkDayRatio] IN (1,2,3,4,5,6,7,8,9,10)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDaysDeductionRatio",
                table: "EmployeeAttendances",
                sql: "[WorkDaysDeductionRatio] IS NULL OR [WorkDaysDeductionRatio] IN (1,2,3,4,5,6,7,8,9,10)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_WorkOverTimeRatio",
                table: "EmployeeAttendances",
                sql: "[WorkOverTimeRatio] IS NULL OR [WorkOverTimeRatio] IN (1,2,3,4,5,6,7,8,9,10)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_WorkPlaceStatus",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDayRatio",
                table: "EmployeeAttendances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDaysDeductionRatio",
                table: "EmployeeAttendances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_WorkOverTimeRatio",
                table: "EmployeeAttendances");

            migrationBuilder.DropColumn(
                name: "IsSalaryMovedToEmployeeAccount",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "PlaceName",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "WorkPlaceStatus",
                table: "Employees");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDayRatio",
                table: "EmployeeAttendances",
                sql: "[WorkDayRatio] IN (1,2,3,4,5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDaysDeductionRatio",
                table: "EmployeeAttendances",
                sql: "[WorkDaysDeductionRatio] IS NULL OR [WorkDaysDeductionRatio] IN (1,2,3,4,5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_WorkOverTimeRatio",
                table: "EmployeeAttendances",
                sql: "[WorkOverTimeRatio] IS NULL OR [WorkOverTimeRatio] IN (1,2,3,4,5)");
        }
    }
}
