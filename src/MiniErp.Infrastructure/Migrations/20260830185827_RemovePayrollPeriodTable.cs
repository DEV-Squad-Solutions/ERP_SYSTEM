using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovePayrollPeriodTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollPeriods");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeMovements_CompanyId_Type_MovementDate",
                table: "EmployeeMovements",
                columns: new[] { "CompanyId", "Type", "MovementDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeMovements_CompanyId_Type_MovementDate",
                table: "EmployeeMovements");

            migrationBuilder.CreateTable(
                name: "PayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false, computedColumnSql: "N'Roll-' + RIGHT(N'000' + CAST([Id] AS NVARCHAR(10)), 3)", stored: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAbsentDays = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalDailyEmployees = table.Column<int>(type: "int", nullable: true),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalEmployees = table.Column<int>(type: "int", nullable: true),
                    TotalGrossSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalMonthlyEmployees = table.Column<int>(type: "int", nullable: true),
                    TotalNetSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalOvertimeDays = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalWorkedDays = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkingDaysInPeriod = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriods", x => x.Id);
                    table.UniqueConstraint("AK_PayrollPeriods_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_PayrollPeriods_Amounts", "([TotalGrossSalary] IS NULL OR [TotalGrossSalary] >= 0) AND ([TotalCredits] IS NULL OR [TotalCredits] >= 0) AND ([TotalDebits] IS NULL OR [TotalDebits] >= 0) AND ([TotalNetSalary] IS NULL OR [TotalNetSalary] >= 0) AND ([TotalWorkedDays] IS NULL OR [TotalWorkedDays] >= 0) AND ([TotalOvertimeDays] IS NULL OR [TotalOvertimeDays] >= 0) AND ([TotalAbsentDays] IS NULL OR [TotalAbsentDays] >= 0)");
                    table.CheckConstraint("CK_PayrollPeriods_Dates", "[StartDate] <= [EndDate]");
                    table.CheckConstraint("CK_PayrollPeriods_EmployeeCounts", "([TotalEmployees] IS NULL OR [TotalEmployees] >= 0) AND ([TotalMonthlyEmployees] IS NULL OR [TotalMonthlyEmployees] >= 0) AND ([TotalDailyEmployees] IS NULL OR [TotalDailyEmployees] >= 0)");
                    table.CheckConstraint("CK_PayrollPeriods_WorkingDays", "[WorkingDaysInPeriod] > 0");
                    table.ForeignKey(
                        name: "FK_PayrollPeriods_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_CompanyId_Code",
                table: "PayrollPeriods",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_CompanyId_Name",
                table: "PayrollPeriods",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_CompanyId_StartDate_EndDate",
                table: "PayrollPeriods",
                columns: new[] { "CompanyId", "StartDate", "EndDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_CompanyId_Status",
                table: "PayrollPeriods",
                columns: new[] { "CompanyId", "Status" });
        }
    }
}
