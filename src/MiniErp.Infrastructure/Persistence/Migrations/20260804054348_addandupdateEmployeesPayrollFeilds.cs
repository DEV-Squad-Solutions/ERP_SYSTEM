using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addandupdateEmployeesPayrollFeilds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false, computedColumnSql: "N'Emp-' + RIGHT(N'000' + CAST([Id] AS NVARCHAR(10)), 3)", stored: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DailySalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RequiredWorkingDaysPerMonth = table.Column<int>(type: "int", nullable: true),
                    LastDayOfReceivingSalary = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeletedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.UniqueConstraint("AK_Employees_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_Employees_RequiredWorkingDays", "[RequiredWorkingDaysPerMonth] IS NULL OR ([RequiredWorkingDaysPerMonth] >= 1 AND [RequiredWorkingDaysPerMonth] <= 31)");
                    table.CheckConstraint("CK_Employees_Salary_NonNegative", "([DailySalary] IS NULL OR [DailySalary] >= 0) AND ([MonthlySalary] IS NULL OR [MonthlySalary] >= 0)");
                    table.CheckConstraint("CK_Employees_SalaryType", "([Type] = 1 AND [DailySalary] IS NOT NULL AND [MonthlySalary] IS NULL) OR ([Type] = 2 AND [MonthlySalary] IS NOT NULL AND [DailySalary] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Employees_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false, computedColumnSql: "N'Roll-' + RIGHT(N'000' + CAST([Id] AS NVARCHAR(10)), 3)", stored: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WorkingDaysInPeriod = table.Column<int>(type: "int", nullable: false),
                    TotalEmployees = table.Column<int>(type: "int", nullable: true),
                    TotalMonthlyEmployees = table.Column<int>(type: "int", nullable: true),
                    TotalDailyEmployees = table.Column<int>(type: "int", nullable: true),
                    TotalGrossSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalNetSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalWorkedDays = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalOvertimeDays = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAbsentDays = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeletedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "EmployeeAttendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckIn = table.Column<TimeOnly>(type: "time", nullable: true),
                    CheckOut = table.Column<TimeOnly>(type: "time", nullable: true),
                    WorkHours = table.Column<TimeOnly>(type: "time", nullable: true),
                    WorkDayRatio = table.Column<int>(type: "int", nullable: false),
                    WorkOverTimeRatio = table.Column<int>(type: "int", nullable: true),
                    WorkDaysDeductionRatio = table.Column<int>(type: "int", nullable: true),
                    WorkLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeletedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAttendances", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeAttendances_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_EmployeeAttendances_CheckOutAfterCheckIn", "[CheckIn] IS NULL OR [CheckOut] IS NULL OR [CheckOut] >= [CheckIn]");
                    table.CheckConstraint("CK_EmployeeAttendances_WorkDayRatio", "[WorkDayRatio] IN (25,33,50,75,100)");
                    table.CheckConstraint("CK_EmployeeAttendances_WorkDaysDeductionRatio", "[WorkDaysDeductionRatio] IS NULL OR [WorkDaysDeductionRatio] IN (25,33,50,75,100)");
                    table.CheckConstraint("CK_EmployeeAttendances_WorkOverTimeRatio", "[WorkOverTimeRatio] IS NULL OR [WorkOverTimeRatio] IN (25,33,50,75,100)");
                    table.ForeignKey(
                        name: "FK_EmployeeAttendances_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeAttendances_Employees_CompanyId_EmployeeId",
                        columns: x => new { x.CompanyId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    PayrollEntryId = table.Column<int>(type: "int", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeletedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTransactions", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeTransactions_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_EmployeeTransactions_Amount_Positive", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeeTransactions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeTransactions_Employees_CompanyId_EmployeeId",
                        columns: x => new { x.CompanyId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    EmployeeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployeeType = table.Column<int>(type: "int", nullable: false),
                    PresentDays = table.Column<int>(type: "int", nullable: false),
                    AbsentDays = table.Column<int>(type: "int", nullable: false),
                    WorkedDays = table.Column<int>(type: "int", nullable: false),
                    Overtimebydayunit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiredWorkingDays = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SalaryPerDay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CalculatedSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsTakeSalary = table.Column<bool>(type: "bit", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NetSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeletedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEntries", x => x.Id);
                    table.UniqueConstraint("AK_PayrollEntries_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_PayrollEntries_Amounts_NonNegative", "[Overtimebydayunit] >= 0 AND [RequiredWorkingDays] >= 0 AND ([SalaryPerDay] IS NULL OR [SalaryPerDay] >= 0) AND [CalculatedSalary] >= 0 AND [TotalCredits] >= 0 AND [TotalDebits] >= 0 AND ([GrossSalary] IS NULL OR [GrossSalary] >= 0) AND ([NetSalary] IS NULL OR [NetSalary] >= 0)");
                    table.CheckConstraint("CK_PayrollEntries_Dates", "[StartDate] <= [EndDate]");
                    table.CheckConstraint("CK_PayrollEntries_Days", "[PresentDays] >= 0 AND [AbsentDays] >= 0 AND [WorkedDays] >= 0");
                    table.ForeignKey(
                        name: "FK_PayrollEntries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEntries_Employees_CompanyId_EmployeeId",
                        columns: x => new { x.CompanyId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendances_CompanyId_EmployeeId_Status",
                table: "EmployeeAttendances",
                columns: new[] { "CompanyId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendances_CompanyId_EmployeeId_WorkDate",
                table: "EmployeeAttendances",
                columns: new[] { "CompanyId", "EmployeeId", "WorkDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendances_CompanyId_WorkDate",
                table: "EmployeeAttendances",
                columns: new[] { "CompanyId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_Code",
                table: "Employees",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_Name",
                table: "Employees",
                columns: new[] { "CompanyId", "Name" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_EmployeeId_TransactionDate_Id",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "EmployeeId", "TransactionDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_EmployeeId_Type_IsProcessed",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "EmployeeId", "Type", "IsProcessed" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_PayrollEntryId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "PayrollEntryId" },
                filter: "[PayrollEntryId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CompanyId_EmployeeId_StartDate_EndDate",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "EmployeeId", "StartDate", "EndDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CompanyId_EmployeeType",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "EmployeeType" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CompanyId_IsTakeSalary",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "IsTakeSalary" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CompanyId_StartDate_EndDate",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "StartDate", "EndDate" });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeAttendances");

            migrationBuilder.DropTable(
                name: "EmployeeTransactions");

            migrationBuilder.DropTable(
                name: "PayrollEntries");

            migrationBuilder.DropTable(
                name: "PayrollPeriods");

            migrationBuilder.DropTable(
                name: "Employees");
        }
    }
}
