using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistCashVoucherClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Classification",
                table: "CashVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE voucher
                SET Classification = movementType.Classification
                FROM CashVouchers AS voucher
                INNER JOIN CashMovementTypes AS movementType
                    ON movementType.CompanyId = voucher.CompanyId
                   AND movementType.Id = voucher.CashMovementTypeId
                WHERE voucher.Classification IS NULL;

                UPDATE voucher
                SET Classification = CASE account.AccountType
                    WHEN 5 THEN 2
                    WHEN 4 THEN 3
                    ELSE NULL
                END
                FROM CashVouchers AS voucher
                INNER JOIN Accounts AS account
                    ON account.CompanyId = voucher.CompanyId
                   AND account.Id = voucher.AccountId
                WHERE account.AccountType IN (4, 5);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Classification",
                table: "CashVouchers");
        }
    }
}
