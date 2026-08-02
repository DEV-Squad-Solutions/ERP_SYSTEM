using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowCashboxOnCashVoucherDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CashVouchers_PostingReferencesTogether",
                table: "CashVouchers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashVouchers_PostingReferencesTogether",
                table: "CashVouchers",
                sql: "[CashMovementTypeId] IS NULL OR [CashboxId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CashVouchers_PostingReferencesTogether",
                table: "CashVouchers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashVouchers_PostingReferencesTogether",
                table: "CashVouchers",
                sql: "([CashboxId] IS NULL AND [CashMovementTypeId] IS NULL) OR ([CashboxId] IS NOT NULL AND [CashMovementTypeId] IS NOT NULL)");
        }
    }
}
