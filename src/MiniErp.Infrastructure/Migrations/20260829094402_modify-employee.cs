using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class modifyemployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollEntries_EmployeeTransactions_EmployeeTransactionId",
                table: "PayrollEntries");

            migrationBuilder.DropTable(
                name: "EmployeeTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PayrollEntries_EmployeeTransactionId",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "EmployeeTransactionId",
                table: "PayrollEntries");

            migrationBuilder.AddColumn<DateOnly>(
                name: "SalaryMovedOn",
                table: "PayrollEntries",
                type: "date",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PayrollEntries_CompanyId_Id",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateTable(
                name: "EmployeeMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    CashVoucherId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false, defaultValue: 1m),
                    BaseDebit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false, defaultValue: 0m),
                    BaseCredit = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false, defaultValue: 0m),
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
                    table.PrimaryKey("PK_EmployeeMovements", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeMovements_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_EmployeeMovements_Amounts_NonNegative", "[Debit] >= 0 AND [Credit] >= 0");
                    table.CheckConstraint("CK_EmployeeMovements_ExactlyOneAmount", "([Debit] > 0 AND [Credit] = 0) OR ([Debit] = 0 AND [Credit] > 0)");
                    table.ForeignKey(
                        name: "FK_EmployeeMovements_CashVouchers_CompanyId_CashVoucherId",
                        columns: x => new { x.CompanyId, x.CashVoucherId },
                        principalTable: "CashVouchers",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeMovements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeMovements_Employees_CompanyId_EmployeeId",
                        columns: x => new { x.CompanyId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeOpeningBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PayrollEntryId = table.Column<int>(type: "int", nullable: true),
                    DocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    ExchangeRateId = table.Column<int>(type: "int", nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false, defaultValue: 1m),
                    BalanceType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false, defaultValue: 0m),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_EmployeeOpeningBalances", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeOpeningBalances_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_EmployeeOpeningBalances_Amount_Positive", "[Amount] > 0");
                    table.CheckConstraint("CK_EmployeeOpeningBalances_Currency_EGP", "[Currency] = 1");
                    table.ForeignKey(
                        name: "FK_EmployeeOpeningBalances_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeOpeningBalances_Employees_CompanyId_EmployeeId",
                        columns: x => new { x.CompanyId, x.EmployeeId },
                        principalTable: "Employees",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeOpeningBalances_ExchangeRates_CompanyId_ExchangeRateId",
                        columns: x => new { x.CompanyId, x.ExchangeRateId },
                        principalTable: "ExchangeRates",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeOpeningBalances_PayrollEntries_CompanyId_PayrollEntryId",
                        columns: x => new { x.CompanyId, x.PayrollEntryId },
                        principalTable: "PayrollEntries",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeAttendances_EmployeeAttendanceStatus",
                table: "EmployeeAttendances",
                sql: "[Status] IN (0,1)");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeMovements_CompanyId_CashVoucherId",
                table: "EmployeeMovements",
                columns: new[] { "CompanyId", "CashVoucherId" },
                unique: true,
                filter: "[CashVoucherId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeMovements_CompanyId_EmployeeId_Currency_MovementDate_Id",
                table: "EmployeeMovements",
                columns: new[] { "CompanyId", "EmployeeId", "Currency", "MovementDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOpeningBalances_CompanyId_DocumentNumber",
                table: "EmployeeOpeningBalances",
                columns: new[] { "CompanyId", "DocumentNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOpeningBalances_CompanyId_EmployeeId",
                table: "EmployeeOpeningBalances",
                columns: new[] { "CompanyId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOpeningBalances_CompanyId_ExchangeRateId",
                table: "EmployeeOpeningBalances",
                columns: new[] { "CompanyId", "ExchangeRateId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOpeningBalances_CompanyId_PayrollEntryId",
                table: "EmployeeOpeningBalances",
                columns: new[] { "CompanyId", "PayrollEntryId" },
                unique: true,
                filter: "[PayrollEntryId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeMovements");

            migrationBuilder.DropTable(
                name: "EmployeeOpeningBalances");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PayrollEntries_CompanyId_Id",
                table: "PayrollEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeAttendances_EmployeeAttendanceStatus",
                table: "EmployeeAttendances");

            migrationBuilder.DropColumn(
                name: "SalaryMovedOn",
                table: "PayrollEntries");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeTransactionId",
                table: "PayrollEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
                    CashVoucherId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RunningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedByPc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTransactions", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeTransactions_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_EmployeeTransactions_Amount_Positive", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeeTransactions_CashVouchers_CompanyId_CashVoucherId",
                        columns: x => new { x.CompanyId, x.CashVoucherId },
                        principalTable: "CashVouchers",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeTransactions_Cashboxes_CompanyId_CashBoxId",
                        columns: x => new { x.CompanyId, x.CashBoxId },
                        principalTable: "Cashboxes",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_EmployeeTransactionId",
                table: "PayrollEntries",
                column: "EmployeeTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_CashBoxId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "CashBoxId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_CashVoucherId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "CashVoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_EmployeeId_TransactionDate_Id",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "EmployeeId", "TransactionDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_EmployeeId_Type",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "EmployeeId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_CompanyId_SourceType_SourceId",
                table: "EmployeeTransactions",
                columns: new[] { "CompanyId", "SourceType", "SourceId" },
                filter: "[SourceId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollEntries_EmployeeTransactions_EmployeeTransactionId",
                table: "PayrollEntries",
                column: "EmployeeTransactionId",
                principalTable: "EmployeeTransactions",
                principalColumn: "Id");
        }
    }
}
