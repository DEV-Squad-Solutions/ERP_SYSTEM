using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashVoucherEmployeeParty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CashVouchers_PartyShape",
                table: "CashVouchers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashVouchers_PartyType",
                table: "CashVouchers");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "CashVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_EmployeeId_VoucherDate_Id",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "EmployeeId", "VoucherDate", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashVouchers_PartyShape",
                table: "CashVouchers",
                sql: "([PartyType] = 1 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL) OR ([PartyType] = 2 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NOT NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL) OR ([PartyType] = 3 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NULL AND [DriverId] IS NOT NULL AND [ExternalPartyName] IS NULL) OR ([PartyType] = 4 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NOT NULL) OR ([PartyType] = 5 AND [EmployeeId] IS NOT NULL AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashVouchers_PartyType",
                table: "CashVouchers",
                sql: "[PartyType] IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddForeignKey(
                name: "FK_CashVouchers_Employees_CompanyId_EmployeeId",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "EmployeeId" },
                principalTable: "Employees",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashVouchers_Employees_CompanyId_EmployeeId",
                table: "CashVouchers");

            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_CompanyId_EmployeeId_VoucherDate_Id",
                table: "CashVouchers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashVouchers_PartyShape",
                table: "CashVouchers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashVouchers_PartyType",
                table: "CashVouchers");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "CashVouchers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashVouchers_PartyShape",
                table: "CashVouchers",
                sql: "([PartyType] = 1 AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL) OR ([PartyType] = 2 AND [BusinessPartnerId] IS NOT NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL) OR ([PartyType] = 3 AND [BusinessPartnerId] IS NULL AND [DriverId] IS NOT NULL AND [ExternalPartyName] IS NULL) OR ([PartyType] = 4 AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashVouchers_PartyType",
                table: "CashVouchers",
                sql: "[PartyType] IN (1, 2, 3, 4)");
        }
    }
}
