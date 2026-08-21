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

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDayRatio",
                table: "EmployeeAttendances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_WorkDaysDeductionRatio",
                table: "EmployeeAttendances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_WorkOverTimeRatio",
                table: "EmployeeAttendances");

            migrationBuilder.Sql(
                """
                UPDATE [EmployeeAttendances]
                SET [WorkDayRatio] = CASE [WorkDayRatio]
                    WHEN 100 THEN 1
                    WHEN 75 THEN 2
                    WHEN 50 THEN 3
                    WHEN 33 THEN 4
                    WHEN 25 THEN 5
                    WHEN 1 THEN 5
                    ELSE [WorkDayRatio]
                END
                WHERE [WorkDayRatio] IN (100, 75, 50, 33, 25, 1);

                UPDATE [EmployeeAttendances]
                SET [WorkDaysDeductionRatio] = CASE [WorkDaysDeductionRatio]
                    WHEN 100 THEN 1
                    WHEN 75 THEN 2
                    WHEN 50 THEN 3
                    WHEN 33 THEN 4
                    WHEN 25 THEN 5
                    WHEN 1 THEN 5
                    ELSE [WorkDaysDeductionRatio]
                END
                WHERE [WorkDaysDeductionRatio] IN (100, 75, 50, 33, 25, 1);

                UPDATE [EmployeeAttendances]
                SET [WorkOverTimeRatio] = CASE [WorkOverTimeRatio]
                    WHEN 100 THEN 1
                    WHEN 75 THEN 2
                    WHEN 50 THEN 3
                    WHEN 33 THEN 4
                    WHEN 25 THEN 5
                    WHEN 1 THEN 5
                    ELSE [WorkOverTimeRatio]
                END
                WHERE [WorkOverTimeRatio] IN (100, 75, 50, 33, 25, 1);
                """);

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

            migrationBuilder.Sql(
                """
                UPDATE [EmployeeAttendances]
                SET [WorkDayRatio] = CASE [WorkDayRatio]
                    WHEN 1 THEN 100
                    WHEN 2 THEN 75
                    WHEN 3 THEN 50
                    WHEN 4 THEN 33
                    WHEN 5 THEN 25
                    ELSE [WorkDayRatio]
                END
                WHERE [WorkDayRatio] IN (1, 2, 3, 4, 5);

                UPDATE [EmployeeAttendances]
                SET [WorkDaysDeductionRatio] = CASE [WorkDaysDeductionRatio]
                    WHEN 1 THEN 100
                    WHEN 2 THEN 75
                    WHEN 3 THEN 50
                    WHEN 4 THEN 33
                    WHEN 5 THEN 25
                    ELSE [WorkDaysDeductionRatio]
                END
                WHERE [WorkDaysDeductionRatio] IN (1, 2, 3, 4, 5);

                UPDATE [EmployeeAttendances]
                SET [WorkOverTimeRatio] = CASE [WorkOverTimeRatio]
                    WHEN 1 THEN 100
                    WHEN 2 THEN 75
                    WHEN 3 THEN 50
                    WHEN 4 THEN 33
                    WHEN 5 THEN 25
                    ELSE [WorkOverTimeRatio]
                END
                WHERE [WorkOverTimeRatio] IN (1, 2, 3, 4, 5);
                """);

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
