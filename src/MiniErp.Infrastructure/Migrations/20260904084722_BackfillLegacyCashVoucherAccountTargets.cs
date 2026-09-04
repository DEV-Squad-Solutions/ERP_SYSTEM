using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLegacyCashVoucherAccountTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE voucher
                SET
                    voucher.AccountId = mapping.AccountId,
                    voucher.PartyType = 1
                FROM CashVouchers AS voucher
                INNER JOIN FiscalYears AS fiscalYear
                    ON fiscalYear.CompanyId = voucher.CompanyId
                    AND voucher.VoucherDate BETWEEN fiscalYear.StartDate AND fiscalYear.EndDate
                    AND fiscalYear.IsDeleted = 0
                INNER JOIN AccountMappings AS mapping
                    ON mapping.CompanyId = voucher.CompanyId
                    AND mapping.FiscalYearId = fiscalYear.Id
                    AND mapping.MappingType = 2
                    AND mapping.SourceId = voucher.CashMovementTypeId
                    AND mapping.IsDeleted = 0
                INNER JOIN Accounts AS account
                    ON account.CompanyId = voucher.CompanyId
                    AND account.Id = mapping.AccountId
                    AND account.IsDeleted = 0
                    AND account.IsActive = 1
                    AND account.IsPosting = 1
                    AND (
                        (voucher.Direction = 1 AND account.AccountType = 4) OR
                        (voucher.Direction = 2 AND account.AccountType = 5)
                    )
                WHERE voucher.IsDeleted = 0
                    AND voucher.IsPosted = 1
                    AND voucher.InvoiceId IS NULL
                    AND voucher.CashboxTransferId IS NULL
                    AND voucher.CashMovementTypeId IS NOT NULL
                    AND voucher.AccountId IS NULL
                    AND voucher.BusinessPartnerId IS NULL
                    AND voucher.EmployeeId IS NULL
                    AND voucher.DriverId IS NULL
                    AND NULLIF(LTRIM(RTRIM(voucher.ExternalPartyName)), '') IS NULL;

                IF EXISTS (
                    SELECT 1
                    FROM CashVouchers AS voucher
                    WHERE voucher.IsDeleted = 0
                        AND voucher.IsPosted = 1
                        AND voucher.InvoiceId IS NULL
                        AND voucher.CashboxTransferId IS NULL
                        AND voucher.CashMovementTypeId IS NOT NULL
                        AND voucher.AccountId IS NULL
                        AND voucher.BusinessPartnerId IS NULL
                        AND voucher.EmployeeId IS NULL
                        AND voucher.DriverId IS NULL
                        AND NULLIF(LTRIM(RTRIM(voucher.ExternalPartyName)), '') IS NULL
                )
                BEGIN
                    THROW 51000,
                        'A legacy cash voucher could not be linked to its mapped posting account.',
                        1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data repair is intentionally not reversed. Clearing AccountId here
            // could erase an account target selected by a user after the migration.
        }
    }
}
