using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashboxJournalLineParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntryLines_PartyShape",
                table: "JournalEntryLines");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntryLines_PartyShape",
                table: "JournalEntryLines",
                sql: "(([PartyType] IS NULL AND [PartyId] IS NULL) OR ([PartyType] IN (1, 2, 3, 4, 5) AND [PartyId] IS NOT NULL))");

            // Backfill only unambiguous cashbox targets; existing party data is preserved.
            migrationBuilder.Sql(
                """
                UPDATE line
                SET PartyType = 5,
                    PartyId = voucher.CashboxId
                FROM JournalEntryLines line
                INNER JOIN JournalEntries entry
                    ON entry.CompanyId = line.CompanyId
                   AND entry.Id = line.JournalEntryId
                INNER JOIN CashVouchers voucher
                    ON voucher.CompanyId = entry.CompanyId
                   AND voucher.Id = entry.SourceId
                INNER JOIN AccountMappings mapping
                    ON mapping.CompanyId = entry.CompanyId
                   AND mapping.FiscalYearId = entry.FiscalYearId
                   AND mapping.AccountId = line.AccountId
                   AND mapping.MappingType = 1
                   AND mapping.SourceId = voucher.CashboxId
                   AND mapping.IsDeleted = 0
                WHERE entry.SourceType = 2
                  AND voucher.CashboxId IS NOT NULL
                  AND entry.IsDeleted = 0
                  AND line.IsDeleted = 0
                  AND line.PartyType IS NULL
                  AND line.PartyId IS NULL;

                UPDATE line
                SET PartyType = 5,
                    PartyId = CASE
                        WHEN line.Debit > 0 THEN transfer.DestinationCashboxId
                        ELSE transfer.SourceCashboxId END
                FROM JournalEntryLines line
                INNER JOIN JournalEntries entry
                    ON entry.CompanyId = line.CompanyId
                   AND entry.Id = line.JournalEntryId
                INNER JOIN CashboxTransfers transfer
                    ON transfer.CompanyId = entry.CompanyId
                   AND transfer.Id = entry.SourceId
                INNER JOIN AccountMappings mapping
                    ON mapping.CompanyId = entry.CompanyId
                   AND mapping.FiscalYearId = entry.FiscalYearId
                   AND mapping.AccountId = line.AccountId
                   AND mapping.MappingType = 1
                   AND mapping.IsDeleted = 0
                WHERE entry.SourceType = 3
                  AND entry.IsDeleted = 0
                  AND line.IsDeleted = 0
                  AND line.PartyType IS NULL
                  AND line.PartyId IS NULL
                  AND ((line.Debit > 0 AND mapping.SourceId = transfer.DestinationCashboxId)
                       OR (line.Credit > 0 AND mapping.SourceId = transfer.SourceCashboxId));

                UPDATE line
                SET PartyType = 5,
                    PartyId = cashbox.Id
                FROM JournalEntryLines line
                INNER JOIN JournalEntries entry
                    ON entry.CompanyId = line.CompanyId
                   AND entry.Id = line.JournalEntryId
                INNER JOIN Cashboxes cashbox
                    ON cashbox.CompanyId = entry.CompanyId
                   AND cashbox.Id = entry.SourceId
                INNER JOIN AccountMappings mapping
                    ON mapping.CompanyId = entry.CompanyId
                   AND mapping.FiscalYearId = entry.FiscalYearId
                   AND mapping.AccountId = line.AccountId
                   AND mapping.MappingType = 1
                   AND mapping.SourceId = cashbox.Id
                   AND mapping.IsDeleted = 0
                WHERE entry.SourceType = 11
                  AND entry.IsDeleted = 0
                  AND line.IsDeleted = 0
                  AND line.PartyType IS NULL
                  AND line.PartyId IS NULL;

                ;WITH InvoiceCashboxes AS
                (
                    SELECT payment.CompanyId,
                           payment.InvoiceId,
                           MIN(cashVoucher.CashboxId) AS CashboxId,
                           COUNT(DISTINCT cashVoucher.CashboxId) AS CashboxCount
                    FROM InvoicePayments payment
                    INNER JOIN CashVouchers cashVoucher
                        ON cashVoucher.CompanyId = payment.CompanyId
                       AND cashVoucher.Id = payment.CashVoucherId
                    WHERE payment.IsDeleted = 0
                      AND cashVoucher.IsDeleted = 0
                      AND cashVoucher.CashboxId IS NOT NULL
                    GROUP BY payment.CompanyId, payment.InvoiceId
                )
                UPDATE line
                SET PartyType = 5,
                    PartyId = invoiceCashbox.CashboxId
                FROM JournalEntryLines line
                INNER JOIN JournalEntries entry
                    ON entry.CompanyId = line.CompanyId
                   AND entry.Id = line.JournalEntryId
                INNER JOIN InvoiceCashboxes invoiceCashbox
                    ON invoiceCashbox.CompanyId = entry.CompanyId
                   AND invoiceCashbox.InvoiceId = entry.SourceId
                   AND invoiceCashbox.CashboxCount = 1
                INNER JOIN AccountMappings mapping
                    ON mapping.CompanyId = entry.CompanyId
                   AND mapping.FiscalYearId = entry.FiscalYearId
                   AND mapping.AccountId = line.AccountId
                   AND mapping.MappingType = 1
                   AND mapping.SourceId = invoiceCashbox.CashboxId
                   AND mapping.IsDeleted = 0
                WHERE entry.SourceType = 1
                  AND entry.IsDeleted = 0
                  AND line.IsDeleted = 0
                  AND line.PartyType IS NULL
                  AND line.PartyId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntryLines_PartyShape",
                table: "JournalEntryLines");

            migrationBuilder.Sql(
                """
                UPDATE JournalEntryLines
                SET PartyType = NULL,
                    PartyId = NULL
                WHERE PartyType = 5;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntryLines_PartyShape",
                table: "JournalEntryLines",
                sql: "(([PartyType] IS NULL AND [PartyId] IS NULL) OR ([PartyType] IN (1, 2, 3, 4) AND [PartyId] IS NOT NULL))");
        }
    }
}
