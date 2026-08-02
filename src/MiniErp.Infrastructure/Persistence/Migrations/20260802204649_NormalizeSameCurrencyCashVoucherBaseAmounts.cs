using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeSameCurrencyCashVoucherBaseAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE voucher
                SET voucher.[ExchangeRateId] = NULL,
                    voucher.[ExchangeRate] = 1,
                    voucher.[BaseAmount] = voucher.[Amount]
                FROM [CashVouchers] AS voucher
                LEFT JOIN [CompanySettings] AS settings
                    ON settings.[CompanyId] = voucher.[CompanyId]
                WHERE voucher.[CashMovementTypeId] IS NOT NULL
                  AND voucher.[Currency] = COALESCE(settings.[BaseCurrency], 1)
                  AND (voucher.[ExchangeRate] <> 1
                       OR voucher.[BaseAmount] <> voucher.[Amount]);

                UPDATE movement
                SET movement.[ExchangeRate] = 1,
                    movement.[BaseDebit] = movement.[Debit],
                    movement.[BaseCredit] = movement.[Credit]
                FROM [BusinessPartnerMovements] AS movement
                INNER JOIN [CashVouchers] AS voucher
                    ON voucher.[CompanyId] = movement.[CompanyId]
                   AND voucher.[Id] = movement.[CashVoucherId]
                LEFT JOIN [CompanySettings] AS settings
                    ON settings.[CompanyId] = voucher.[CompanyId]
                WHERE voucher.[CashMovementTypeId] IS NOT NULL
                  AND voucher.[Currency] = COALESCE(settings.[BaseCurrency], 1)
                  AND (movement.[ExchangeRate] <> 1
                       OR movement.[BaseDebit] <> movement.[Debit]
                       OR movement.[BaseCredit] <> movement.[Credit]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Corrected historical monetary snapshots cannot be reconstructed
            // safely, so this data-only migration is intentionally irreversible.
        }
    }
}
