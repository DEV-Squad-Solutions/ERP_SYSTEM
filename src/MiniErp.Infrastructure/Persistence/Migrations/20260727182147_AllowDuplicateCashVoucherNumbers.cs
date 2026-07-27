using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowDuplicateCashVoucherNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_CompanyId_VoucherNumber",
                table: "CashVouchers");

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_VoucherNumber",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "VoucherNumber" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashVouchers_CompanyId_VoucherNumber",
                table: "CashVouchers");

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_VoucherNumber",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "VoucherNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
