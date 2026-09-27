using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalYearOwnershipToTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "StockTransfers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "StockOpeningBalances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "StockAdjustments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "PayrollEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "PartnerOpeningBalances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "MonetaryAccountRevaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "ItemMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "InvoicePayments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "InventoryCounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "EmployeeOpeningBalances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "EmployeeMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "EmployeeAttendances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "DriverTrips",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "ContainerMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "CashVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "CashboxTransfers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "CashboxRevaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FiscalYearId",
                table: "BusinessPartnerMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            BackfillFiscalYear(
                migrationBuilder,
                "Invoices",
                "InvoiceDate");
            BackfillFiscalYear(
                migrationBuilder,
                "CashVouchers",
                "VoucherDate");
            BackfillFiscalYear(
                migrationBuilder,
                "CashboxTransfers",
                "TransferDate");
            BackfillFiscalYear(
                migrationBuilder,
                "CashboxRevaluations",
                "RevaluationDate");
            BackfillFiscalYear(
                migrationBuilder,
                "MonetaryAccountRevaluations",
                "RevaluationDate");
            BackfillFiscalYear(
                migrationBuilder,
                "PartnerOpeningBalances",
                "DocumentDate");
            BackfillFiscalYear(
                migrationBuilder,
                "BusinessPartnerMovements",
                "MovementDate");
            BackfillFiscalYear(
                migrationBuilder,
                "EmployeeOpeningBalances",
                "DocumentDate");
            BackfillFiscalYear(
                migrationBuilder,
                "EmployeeMovements",
                "MovementDate");
            RepairLegacyAttendanceDates(migrationBuilder);
            BackfillFiscalYear(
                migrationBuilder,
                "EmployeeAttendances",
                "WorkDate");
            BackfillFiscalYear(
                migrationBuilder,
                "PayrollEntries",
                "StartDate");
            BackfillFiscalYear(
                migrationBuilder,
                "DriverTrips",
                "TripDate");
            BackfillFiscalYear(
                migrationBuilder,
                "StockOpeningBalances",
                "DocumentDate");
            BackfillFiscalYear(
                migrationBuilder,
                "StockAdjustments",
                "DocumentDate");
            BackfillFiscalYear(
                migrationBuilder,
                "StockTransfers",
                "TransferDate");
            BackfillFiscalYear(
                migrationBuilder,
                "InventoryCounts",
                "CountDate");
            BackfillFiscalYear(
                migrationBuilder,
                "ItemMovements",
                "MovementDate");
            BackfillFiscalYear(
                migrationBuilder,
                "ContainerMovements",
                "MovementDate");

            migrationBuilder.Sql(
                """
                UPDATE payment
                SET payment.FiscalYearId = voucher.FiscalYearId
                FROM InvoicePayments AS payment
                INNER JOIN CashVouchers AS voucher
                    ON voucher.CompanyId = payment.CompanyId
                    AND voucher.Id = payment.CashVoucherId;

                IF EXISTS (
                    SELECT 1
                    FROM InvoicePayments
                    WHERE FiscalYearId = 0)
                BEGIN
                    THROW 51001,
                        'Cannot assign a fiscal year to every InvoicePayments row.',
                        1;
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_Company_FiscalYear",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockOpeningBalances_Company_FiscalYear",
                table: "StockOpeningBalances",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Company_FiscalYear",
                table: "StockAdjustments",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_Company_FiscalYear",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerOpeningBalances_Company_FiscalYear",
                table: "PartnerOpeningBalances",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_MonetaryAccountRevaluations_Company_FiscalYear",
                table: "MonetaryAccountRevaluations",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovements_Company_FiscalYear",
                table: "ItemMovements",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Company_FiscalYear",
                table: "Invoices",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_Company_FiscalYear",
                table: "InvoicePayments",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_Company_FiscalYear",
                table: "InventoryCounts",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOpeningBalances_Company_FiscalYear",
                table: "EmployeeOpeningBalances",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeMovements_Company_FiscalYear",
                table: "EmployeeMovements",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendances_Company_FiscalYear",
                table: "EmployeeAttendances",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverTrips_Company_FiscalYear",
                table: "DriverTrips",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_Company_FiscalYear",
                table: "ContainerMovements",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_Company_FiscalYear",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashboxTransfers_Company_FiscalYear",
                table: "CashboxTransfers",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashboxRevaluations_Company_FiscalYear",
                table: "CashboxRevaluations",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerMovements_Company_FiscalYear",
                table: "BusinessPartnerMovements",
                columns: new[] { "CompanyId", "FiscalYearId" });

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartnerMovements_FiscalYears_CompanyId_FiscalYearId",
                table: "BusinessPartnerMovements",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashboxRevaluations_FiscalYears_CompanyId_FiscalYearId",
                table: "CashboxRevaluations",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashboxTransfers_FiscalYears_CompanyId_FiscalYearId",
                table: "CashboxTransfers",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashVouchers_FiscalYears_CompanyId_FiscalYearId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContainerMovements_FiscalYears_CompanyId_FiscalYearId",
                table: "ContainerMovements",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DriverTrips_FiscalYears_CompanyId_FiscalYearId",
                table: "DriverTrips",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeAttendances_FiscalYears_CompanyId_FiscalYearId",
                table: "EmployeeAttendances",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeMovements_FiscalYears_CompanyId_FiscalYearId",
                table: "EmployeeMovements",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeOpeningBalances_FiscalYears_CompanyId_FiscalYearId",
                table: "EmployeeOpeningBalances",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCounts_FiscalYears_CompanyId_FiscalYearId",
                table: "InventoryCounts",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoicePayments_FiscalYears_CompanyId_FiscalYearId",
                table: "InvoicePayments",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_FiscalYears_CompanyId_FiscalYearId",
                table: "Invoices",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemMovements_FiscalYears_CompanyId_FiscalYearId",
                table: "ItemMovements",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MonetaryAccountRevaluations_FiscalYears_CompanyId_FiscalYearId",
                table: "MonetaryAccountRevaluations",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerOpeningBalances_FiscalYears_CompanyId_FiscalYearId",
                table: "PartnerOpeningBalances",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollEntries_FiscalYears_CompanyId_FiscalYearId",
                table: "PayrollEntries",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockAdjustments_FiscalYears_CompanyId_FiscalYearId",
                table: "StockAdjustments",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockOpeningBalances_FiscalYears_CompanyId_FiscalYearId",
                table: "StockOpeningBalances",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransfers_FiscalYears_CompanyId_FiscalYearId",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "FiscalYearId" },
                principalTable: "FiscalYears",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartnerMovements_FiscalYears_CompanyId_FiscalYearId",
                table: "BusinessPartnerMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_CashboxRevaluations_FiscalYears_CompanyId_FiscalYearId",
                table: "CashboxRevaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_CashboxTransfers_FiscalYears_CompanyId_FiscalYearId",
                table: "CashboxTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_CashVouchers_FiscalYears_CompanyId_FiscalYearId",
                table: "CashVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_ContainerMovements_FiscalYears_CompanyId_FiscalYearId",
                table: "ContainerMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_DriverTrips_FiscalYears_CompanyId_FiscalYearId",
                table: "DriverTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeAttendances_FiscalYears_CompanyId_FiscalYearId",
                table: "EmployeeAttendances");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeMovements_FiscalYears_CompanyId_FiscalYearId",
                table: "EmployeeMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeOpeningBalances_FiscalYears_CompanyId_FiscalYearId",
                table: "EmployeeOpeningBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCounts_FiscalYears_CompanyId_FiscalYearId",
                table: "InventoryCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoicePayments_FiscalYears_CompanyId_FiscalYearId",
                table: "InvoicePayments");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_FiscalYears_CompanyId_FiscalYearId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemMovements_FiscalYears_CompanyId_FiscalYearId",
                table: "ItemMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_MonetaryAccountRevaluations_FiscalYears_CompanyId_FiscalYearId",
                table: "MonetaryAccountRevaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerOpeningBalances_FiscalYears_CompanyId_FiscalYearId",
                table: "PartnerOpeningBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollEntries_FiscalYears_CompanyId_FiscalYearId",
                table: "PayrollEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAdjustments_FiscalYears_CompanyId_FiscalYearId",
                table: "StockAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_StockOpeningBalances_FiscalYears_CompanyId_FiscalYearId",
                table: "StockOpeningBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransfers_FiscalYears_CompanyId_FiscalYearId",
                table: "StockTransfers");

            migrationBuilder.DropIndex(
                name: "IX_StockTransfers_Company_FiscalYear",
                table: "StockTransfers");

            migrationBuilder.DropIndex(
                name: "IX_StockOpeningBalances_Company_FiscalYear",
                table: "StockOpeningBalances");

            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_Company_FiscalYear",
                table: "StockAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_PayrollEntries_Company_FiscalYear",
                table: "PayrollEntries");

            migrationBuilder.DropIndex(
                name: "IX_PartnerOpeningBalances_Company_FiscalYear",
                table: "PartnerOpeningBalances");

            migrationBuilder.DropIndex(
                name: "IX_MonetaryAccountRevaluations_Company_FiscalYear",
                table: "MonetaryAccountRevaluations");

            migrationBuilder.DropIndex(
                name: "IX_ItemMovements_Company_FiscalYear",
                table: "ItemMovements");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Company_FiscalYear",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoicePayments_Company_FiscalYear",
                table: "InvoicePayments");

            migrationBuilder.DropIndex(
                name: "IX_InventoryCounts_Company_FiscalYear",
                table: "InventoryCounts");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeOpeningBalances_Company_FiscalYear",
                table: "EmployeeOpeningBalances");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeMovements_Company_FiscalYear",
                table: "EmployeeMovements");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeAttendances_Company_FiscalYear",
                table: "EmployeeAttendances");

            migrationBuilder.DropIndex(
                name: "IX_DriverTrips_Company_FiscalYear",
                table: "DriverTrips");

            migrationBuilder.DropIndex(
                name: "IX_ContainerMovements_Company_FiscalYear",
                table: "ContainerMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_Company_FiscalYear",
                table: "CashVouchers");

            migrationBuilder.DropIndex(
                name: "IX_CashboxTransfers_Company_FiscalYear",
                table: "CashboxTransfers");

            migrationBuilder.DropIndex(
                name: "IX_CashboxRevaluations_Company_FiscalYear",
                table: "CashboxRevaluations");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartnerMovements_Company_FiscalYear",
                table: "BusinessPartnerMovements");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "StockTransfers");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "StockOpeningBalances");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "PartnerOpeningBalances");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "MonetaryAccountRevaluations");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "ItemMovements");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "InventoryCounts");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "EmployeeOpeningBalances");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "EmployeeMovements");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "EmployeeAttendances");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "DriverTrips");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "ContainerMovements");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "CashboxTransfers");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "CashboxRevaluations");

            migrationBuilder.DropColumn(
                name: "FiscalYearId",
                table: "BusinessPartnerMovements");
        }

        private static void BackfillFiscalYear(
            MigrationBuilder migrationBuilder,
            string table,
            string dateColumn)
        {
            migrationBuilder.Sql(
                $"""
                IF EXISTS (
                    SELECT 1
                    FROM [{table}] AS movement
                    OUTER APPLY (
                        SELECT COUNT_BIG(*) AS MatchCount
                        FROM FiscalYears AS fiscalYear
                        WHERE fiscalYear.CompanyId = movement.CompanyId
                            AND movement.[{dateColumn}]
                                BETWEEN fiscalYear.StartDate AND fiscalYear.EndDate
                    ) AS matches
                    WHERE matches.MatchCount <> 1)
                BEGIN
                    THROW 51000,
                        'Cannot assign exactly one fiscal year to every {table} row.',
                        1;
                END;

                UPDATE movement
                SET movement.FiscalYearId = fiscalYear.Id
                FROM [{table}] AS movement
                INNER JOIN FiscalYears AS fiscalYear
                    ON fiscalYear.CompanyId = movement.CompanyId
                    AND movement.[{dateColumn}]
                        BETWEEN fiscalYear.StartDate AND fiscalYear.EndDate;
                """);
        }

        private static void RepairLegacyAttendanceDates(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE attendance
                SET attendance.IsDeleted = 1,
                    attendance.DeletedOn = COALESCE(
                        attendance.DeletedOn,
                        SYSUTCDATETIME()),
                    attendance.DeletedById = COALESCE(
                        attendance.DeletedById,
                        'fiscal-year-migration'),
                    attendance.DeletedByPc = COALESCE(
                        attendance.DeletedByPc,
                        'fiscal-year-migration')
                FROM EmployeeAttendances AS attendance
                CROSS APPLY (
                    SELECT TRY_CONVERT(
                        date,
                        CONCAT(
                            YEAR(attendance.CreatedOn),
                            '-',
                            RIGHT(
                                '0' + CONVERT(
                                    varchar(2),
                                    MONTH(attendance.WorkDate)),
                                2),
                            '-',
                            RIGHT(
                                '0' + CONVERT(
                                    varchar(2),
                                    DAY(attendance.WorkDate)),
                                2))) AS WorkDate
                ) AS candidate
                WHERE attendance.IsDeleted = 0
                    AND YEAR(attendance.WorkDate) < 1900
                    AND DATEDIFF(
                        DAY,
                        candidate.WorkDate,
                        CAST(attendance.CreatedOn AS date)) BETWEEN 0 AND 31
                    AND 1 = (
                        SELECT COUNT_BIG(*)
                        FROM FiscalYears AS fiscalYear
                        WHERE fiscalYear.CompanyId = attendance.CompanyId
                            AND candidate.WorkDate BETWEEN
                                fiscalYear.StartDate AND fiscalYear.EndDate)
                    AND EXISTS (
                        SELECT 1
                        FROM EmployeeAttendances AS existing
                        WHERE existing.CompanyId = attendance.CompanyId
                            AND existing.EmployeeId = attendance.EmployeeId
                            AND existing.WorkDate = candidate.WorkDate
                            AND existing.IsDeleted = 0
                            AND existing.Id <> attendance.Id);

                UPDATE attendance
                SET attendance.WorkDate = candidate.WorkDate
                FROM EmployeeAttendances AS attendance
                CROSS APPLY (
                    SELECT TRY_CONVERT(
                        date,
                        CONCAT(
                            YEAR(attendance.CreatedOn),
                            '-',
                            RIGHT(
                                '0' + CONVERT(
                                    varchar(2),
                                    MONTH(attendance.WorkDate)),
                                2),
                            '-',
                            RIGHT(
                                '0' + CONVERT(
                                    varchar(2),
                                    DAY(attendance.WorkDate)),
                                2))) AS WorkDate
                ) AS candidate
                WHERE YEAR(attendance.WorkDate) < 1900
                    AND DATEDIFF(
                        DAY,
                        candidate.WorkDate,
                        CAST(attendance.CreatedOn AS date)) BETWEEN 0 AND 31
                    AND 1 = (
                        SELECT COUNT_BIG(*)
                        FROM FiscalYears AS fiscalYear
                        WHERE fiscalYear.CompanyId = attendance.CompanyId
                            AND candidate.WorkDate BETWEEN
                                fiscalYear.StartDate AND fiscalYear.EndDate);
                """);
        }
    }
}
