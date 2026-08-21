using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class modifyEployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_CheckOutAfterCheckIn",
                table: "EmployeeAttendances");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_CheckOutAfterCheckIn",
                table: "EmployeeAttendances",
                sql: "[CheckIn] IS NULL OR [CheckOut] IS NULL OR [CheckOut] >= [CheckIn]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDayRatio",
                table: "EmployeeAttendances",
                sql: "[WorkDayRatio] IN (25,33,50,75,100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDaysDeductionRatio",
                table: "EmployeeAttendances",
                sql: "[WorkDaysDeductionRatio] IS NULL OR [WorkDaysDeductionRatio] IN (25,33,50,75,100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_WorkOverTimeRatio",
                table: "EmployeeAttendances",
                sql: "[WorkOverTimeRatio] IS NULL OR [WorkOverTimeRatio] IN (25,33,50,75,100)");
        }
    }
}
