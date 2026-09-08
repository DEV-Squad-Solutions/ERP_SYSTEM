using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryLineParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartyId",
                table: "JournalEntryLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartyType",
                table: "JournalEntryLines",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE Accounts
                SET Name = N'مستحقات الموظفين'
                WHERE Code = N'2200'
                  AND Name = N'مستحقات الموظفين والسائقين'
                  AND IsDeleted = 0;

                INSERT INTO Accounts (
                    CompanyId, Code, Name, ParentAccountId, AccountType,
                    NormalBalance, IsPosting, IsActive,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                SELECT parent.CompanyId, N'2300', N'مستحقات السائقين',
                       parent.Id, 2, 2, 1, 1,
                       N'system', SYSUTCDATETIME(), N'migration', 0
                FROM Accounts parent
                INNER JOIN Accounts employeeAccount
                    ON employeeAccount.CompanyId = parent.CompanyId
                   AND employeeAccount.Code = N'2200'
                   AND employeeAccount.Name = N'مستحقات الموظفين'
                   AND employeeAccount.IsDeleted = 0
                WHERE parent.Code = N'2000'
                  AND parent.IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM Accounts existing
                      WHERE existing.CompanyId = parent.CompanyId
                        AND existing.Code = N'2300'
                        AND existing.IsDeleted = 0);

                UPDATE driverMapping
                SET AccountId = driverAccount.Id,
                    UpdatedById = N'system',
                    UpdatedOn = SYSUTCDATETIME(),
                    UpdatedByPc = N'migration'
                FROM AccountMappings driverMapping
                INNER JOIN Accounts driverAccount
                    ON driverAccount.CompanyId = driverMapping.CompanyId
                   AND driverAccount.Code = N'2300'
                   AND driverAccount.Name = N'مستحقات السائقين'
                   AND driverAccount.IsDeleted = 0
                INNER JOIN Accounts currentAccount
                    ON currentAccount.CompanyId = driverMapping.CompanyId
                   AND currentAccount.Id = driverMapping.AccountId
                LEFT JOIN AccountMappings employeeMapping
                    ON employeeMapping.CompanyId = driverMapping.CompanyId
                   AND employeeMapping.FiscalYearId = driverMapping.FiscalYearId
                   AND employeeMapping.MappingType = 11
                   AND employeeMapping.SourceId IS NULL
                   AND employeeMapping.IsDeleted = 0
                WHERE driverMapping.MappingType = 12
                  AND driverMapping.SourceId IS NULL
                  AND driverMapping.IsDeleted = 0
                  AND currentAccount.Code = N'2200'
                  AND currentAccount.Name = N'مستحقات الموظفين'
                  AND driverMapping.AccountId = employeeMapping.AccountId;

                UPDATE FinancialStatementLines
                SET Name = N'مستحقات الموظفين'
                WHERE StatementType = 1
                  AND Code = N'FP-220'
                  AND Name = N'مستحقات الموظفين والسائقين'
                  AND IsDeleted = 0;

                INSERT INTO FinancialStatementLines (
                    CompanyId, FiscalYearId, StatementType, Code, Name,
                    ParentLineId, DisplayOrder, IsAssignable, IsActive,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                SELECT parent.CompanyId, parent.FiscalYearId, 1,
                       N'FP-230', N'مستحقات السائقين', parent.Id,
                       230, 1, 1, N'system', SYSUTCDATETIME(), N'migration', 0
                FROM FinancialStatementLines parent
                INNER JOIN Accounts driverAccount
                    ON driverAccount.CompanyId = parent.CompanyId
                   AND driverAccount.Code = N'2300'
                   AND driverAccount.Name = N'مستحقات السائقين'
                   AND driverAccount.IsDeleted = 0
                WHERE parent.StatementType = 1
                  AND parent.Code = N'FP-200'
                  AND parent.IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM FinancialStatementLines existing
                      WHERE existing.CompanyId = parent.CompanyId
                        AND existing.FiscalYearId = parent.FiscalYearId
                        AND existing.StatementType = 1
                        AND existing.Code = N'FP-230'
                        AND existing.IsDeleted = 0);

                INSERT INTO AccountStatementMappings (
                    CompanyId, FiscalYearId, StatementType, AccountId,
                    FinancialStatementLineId,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                SELECT account.CompanyId, line.FiscalYearId, 1,
                       account.Id, line.Id,
                       N'system', SYSUTCDATETIME(), N'migration', 0
                FROM Accounts account
                INNER JOIN FinancialStatementLines line
                    ON line.CompanyId = account.CompanyId
                   AND line.StatementType = 1
                   AND line.Code = N'FP-230'
                   AND line.IsDeleted = 0
                WHERE account.Code = N'2300'
                  AND account.Name = N'مستحقات السائقين'
                  AND account.IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM AccountStatementMappings existing
                      WHERE existing.CompanyId = account.CompanyId
                        AND existing.FiscalYearId = line.FiscalYearId
                        AND existing.StatementType = 1
                        AND existing.AccountId = account.Id
                        AND existing.IsDeleted = 0);

                INSERT INTO AccountStatementMappings (
                    CompanyId, FiscalYearId, StatementType, AccountId,
                    FinancialStatementLineId,
                    CreatedById, CreatedOn, CreatedByPc, IsDeleted)
                SELECT account.CompanyId, line.FiscalYearId, 3,
                       account.Id, line.Id,
                       N'system', SYSUTCDATETIME(), N'migration', 0
                FROM Accounts account
                INNER JOIN FinancialStatementLines line
                    ON line.CompanyId = account.CompanyId
                   AND line.StatementType = 3
                   AND line.Code = N'CF-130'
                   AND line.IsDeleted = 0
                WHERE account.Code = N'2300'
                  AND account.Name = N'مستحقات السائقين'
                  AND account.IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM AccountStatementMappings existing
                      WHERE existing.CompanyId = account.CompanyId
                        AND existing.FiscalYearId = line.FiscalYearId
                        AND existing.StatementType = 3
                        AND existing.AccountId = account.Id
                        AND existing.IsDeleted = 0);

                UPDATE line
                SET PartyType = CASE
                        WHEN invoice.InvoiceType IN (1, 3) THEN 1 ELSE 2 END,
                    PartyId = invoice.BusinessPartnerId
                FROM JournalEntryLines line
                INNER JOIN JournalEntries entry
                    ON entry.CompanyId = line.CompanyId
                   AND entry.Id = line.JournalEntryId
                INNER JOIN Invoices invoice
                    ON invoice.CompanyId = entry.CompanyId
                   AND invoice.Id = entry.SourceId
                INNER JOIN AccountMappings mapping
                    ON mapping.CompanyId = entry.CompanyId
                   AND mapping.FiscalYearId = entry.FiscalYearId
                   AND mapping.AccountId = line.AccountId
                   AND mapping.MappingType = CASE
                        WHEN invoice.InvoiceType IN (1, 3) THEN 9 ELSE 10 END
                   AND mapping.SourceId IS NULL
                   AND mapping.IsDeleted = 0
                WHERE entry.SourceType = 1
                  AND entry.IsDeleted = 0
                  AND line.IsDeleted = 0;

                UPDATE line
                SET PartyType = CASE
                        WHEN voucher.PartyType = 2 AND voucher.Direction = 1 THEN 1
                        WHEN voucher.PartyType = 2 THEN 2
                        WHEN voucher.PartyType = 5 THEN 3
                        WHEN voucher.PartyType = 3 THEN 4 END,
                    PartyId = CASE
                        WHEN voucher.PartyType = 2 THEN voucher.BusinessPartnerId
                        WHEN voucher.PartyType = 5 THEN voucher.EmployeeId
                        WHEN voucher.PartyType = 3 THEN voucher.DriverId END
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
                   AND ((voucher.PartyType = 2 AND voucher.Direction = 1
                         AND mapping.MappingType = 9)
                        OR (voucher.PartyType = 2 AND voucher.Direction <> 1
                            AND mapping.MappingType = 10)
                        OR (voucher.PartyType = 5
                            AND mapping.MappingType = 11)
                        OR (voucher.PartyType = 3
                            AND mapping.MappingType IN (11, 12)))
                   AND mapping.SourceId IS NULL
                   AND mapping.IsDeleted = 0
                WHERE entry.SourceType = 2
                  AND voucher.PartyType IN (2, 3, 5)
                  AND ((voucher.PartyType = 2
                        AND voucher.BusinessPartnerId IS NOT NULL)
                       OR (voucher.PartyType = 5
                           AND voucher.EmployeeId IS NOT NULL)
                       OR (voucher.PartyType = 3
                           AND voucher.DriverId IS NOT NULL))
                  AND entry.IsDeleted = 0
                  AND line.IsDeleted = 0;

                UPDATE line
                SET PartyType = CASE
                        WHEN balance.BalanceType = 1 THEN 1 ELSE 2 END,
                    PartyId = balance.BusinessPartnerId
                FROM JournalEntryLines line
                INNER JOIN JournalEntries entry
                    ON entry.CompanyId = line.CompanyId
                   AND entry.Id = line.JournalEntryId
                INNER JOIN PartnerOpeningBalances balance
                    ON balance.CompanyId = entry.CompanyId
                   AND balance.Id = entry.SourceId
                INNER JOIN AccountMappings mapping
                    ON mapping.CompanyId = entry.CompanyId
                   AND mapping.FiscalYearId = entry.FiscalYearId
                   AND mapping.AccountId = line.AccountId
                   AND mapping.MappingType = CASE
                        WHEN balance.BalanceType = 1 THEN 9 ELSE 10 END
                   AND mapping.SourceId IS NULL
                   AND mapping.IsDeleted = 0
                WHERE entry.SourceType = 9
                  AND entry.IsDeleted = 0
                  AND line.IsDeleted = 0;

                UPDATE line
                SET PartyType = 3,
                    PartyId = balance.EmployeeId
                FROM JournalEntryLines line
                INNER JOIN JournalEntries entry
                    ON entry.CompanyId = line.CompanyId
                   AND entry.Id = line.JournalEntryId
                INNER JOIN EmployeeOpeningBalances balance
                    ON balance.CompanyId = entry.CompanyId
                   AND balance.Id = entry.SourceId
                INNER JOIN AccountMappings mapping
                    ON mapping.CompanyId = entry.CompanyId
                   AND mapping.FiscalYearId = entry.FiscalYearId
                   AND mapping.AccountId = line.AccountId
                   AND mapping.MappingType IN (11, 18)
                   AND mapping.SourceId IS NULL
                   AND mapping.IsDeleted = 0
                WHERE entry.SourceType = 10
                  AND entry.IsDeleted = 0
                  AND line.IsDeleted = 0;

                UPDATE line
                SET PartyType = 4,
                    PartyId = trip.DriverId
                FROM JournalEntryLines line
                INNER JOIN JournalEntries entry
                    ON entry.CompanyId = line.CompanyId
                   AND entry.Id = line.JournalEntryId
                INNER JOIN DriverTrips trip
                    ON trip.CompanyId = entry.CompanyId
                   AND trip.Id = entry.SourceId
                INNER JOIN AccountMappings mapping
                    ON mapping.CompanyId = entry.CompanyId
                   AND mapping.FiscalYearId = entry.FiscalYearId
                   AND mapping.AccountId = line.AccountId
                   AND mapping.MappingType IN (11, 12)
                   AND mapping.SourceId IS NULL
                   AND mapping.IsDeleted = 0
                WHERE entry.SourceType = 12
                  AND entry.IsDeleted = 0
                  AND line.IsDeleted = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_Company_Party",
                table: "JournalEntryLines",
                columns: new[] { "CompanyId", "PartyType", "PartyId", "JournalEntryId" },
                filter: "[PartyType] IS NOT NULL AND [PartyId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntryLines_PartyShape",
                table: "JournalEntryLines",
                sql: "(([PartyType] IS NULL AND [PartyId] IS NULL) OR ([PartyType] IN (1, 2, 3, 4) AND [PartyId] IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntryLines_Company_Party",
                table: "JournalEntryLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntryLines_PartyShape",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "PartyType",
                table: "JournalEntryLines");
        }
    }
}
