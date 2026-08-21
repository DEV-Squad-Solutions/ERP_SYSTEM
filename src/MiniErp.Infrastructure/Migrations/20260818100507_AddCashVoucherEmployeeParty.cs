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
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[CK_CashVouchers_PartyShape]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[CashVouchers] DROP CONSTRAINT [CK_CashVouchers_PartyShape];

                IF OBJECT_ID(N'[dbo].[CK_CashVouchers_PartyType]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[CashVouchers] DROP CONSTRAINT [CK_CashVouchers_PartyType];

                IF COL_LENGTH(N'dbo.CashVouchers', N'EmployeeId') IS NULL
                    ALTER TABLE [dbo].[CashVouchers] ADD [EmployeeId] int NULL;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_CashVouchers_CompanyId_EmployeeId_VoucherDate_Id'
                      AND [object_id] = OBJECT_ID(N'[dbo].[CashVouchers]'))
                    CREATE INDEX [IX_CashVouchers_CompanyId_EmployeeId_VoucherDate_Id]
                        ON [dbo].[CashVouchers] ([CompanyId], [EmployeeId], [VoucherDate], [Id]);

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE [name] = N'FK_CashVouchers_Employees_CompanyId_EmployeeId'
                      AND [parent_object_id] = OBJECT_ID(N'[dbo].[CashVouchers]'))
                    ALTER TABLE [dbo].[CashVouchers] WITH CHECK
                    ADD CONSTRAINT [FK_CashVouchers_Employees_CompanyId_EmployeeId]
                        FOREIGN KEY ([CompanyId], [EmployeeId])
                        REFERENCES [dbo].[Employees] ([CompanyId], [Id]);

                IF OBJECT_ID(N'[dbo].[CK_CashVouchers_PartyShape]', N'C') IS NULL
                    ALTER TABLE [dbo].[CashVouchers] WITH CHECK
                    ADD CONSTRAINT [CK_CashVouchers_PartyShape] CHECK (
                        ([PartyType] = 1 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL) OR
                        ([PartyType] = 2 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NOT NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL) OR
                        ([PartyType] = 3 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NULL AND [DriverId] IS NOT NULL AND [ExternalPartyName] IS NULL) OR
                        ([PartyType] = 4 AND [EmployeeId] IS NULL AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NOT NULL) OR
                        ([PartyType] = 5 AND [EmployeeId] IS NOT NULL AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL));

                IF OBJECT_ID(N'[dbo].[CK_CashVouchers_PartyType]', N'C') IS NULL
                    ALTER TABLE [dbo].[CashVouchers] WITH CHECK
                    ADD CONSTRAINT [CK_CashVouchers_PartyType]
                        CHECK ([PartyType] IN (1, 2, 3, 4, 5));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[CK_CashVouchers_PartyShape]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[CashVouchers] DROP CONSTRAINT [CK_CashVouchers_PartyShape];

                IF OBJECT_ID(N'[dbo].[CK_CashVouchers_PartyType]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[CashVouchers] DROP CONSTRAINT [CK_CashVouchers_PartyType];

                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE [name] = N'FK_CashVouchers_Employees_CompanyId_EmployeeId'
                      AND [parent_object_id] = OBJECT_ID(N'[dbo].[CashVouchers]'))
                    ALTER TABLE [dbo].[CashVouchers]
                        DROP CONSTRAINT [FK_CashVouchers_Employees_CompanyId_EmployeeId];

                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_CashVouchers_CompanyId_EmployeeId_VoucherDate_Id'
                      AND [object_id] = OBJECT_ID(N'[dbo].[CashVouchers]'))
                    DROP INDEX [IX_CashVouchers_CompanyId_EmployeeId_VoucherDate_Id]
                        ON [dbo].[CashVouchers];

                IF COL_LENGTH(N'dbo.CashVouchers', N'EmployeeId') IS NOT NULL
                    ALTER TABLE [dbo].[CashVouchers] DROP COLUMN [EmployeeId];

                IF OBJECT_ID(N'[dbo].[CK_CashVouchers_PartyShape]', N'C') IS NULL
                    ALTER TABLE [dbo].[CashVouchers] WITH CHECK
                    ADD CONSTRAINT [CK_CashVouchers_PartyShape] CHECK (
                        ([PartyType] = 1 AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL) OR
                        ([PartyType] = 2 AND [BusinessPartnerId] IS NOT NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NULL) OR
                        ([PartyType] = 3 AND [BusinessPartnerId] IS NULL AND [DriverId] IS NOT NULL AND [ExternalPartyName] IS NULL) OR
                        ([PartyType] = 4 AND [BusinessPartnerId] IS NULL AND [DriverId] IS NULL AND [DriverTripId] IS NULL AND [ExternalPartyName] IS NOT NULL));

                IF OBJECT_ID(N'[dbo].[CK_CashVouchers_PartyType]', N'C') IS NULL
                    ALTER TABLE [dbo].[CashVouchers] WITH CHECK
                    ADD CONSTRAINT [CK_CashVouchers_PartyType]
                        CHECK ([PartyType] IN (1, 2, 3, 4));
                """);
        }
    }
}
