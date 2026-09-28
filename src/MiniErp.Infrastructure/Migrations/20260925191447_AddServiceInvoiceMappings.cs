using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceInvoiceMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountMappings_MappingType",
                table: "AccountMappings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountMappings_MappingType",
                table: "AccountMappings",
                sql: "[MappingType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23)");

            migrationBuilder.Sql(
                """
                INSERT INTO [Accounts]
                    ([CompanyId], [Code], [Name], [ParentAccountId],
                     [AccountType], [NormalBalance], [IsPosting], [IsActive],
                     [CreatedById], [CreatedOn], [CreatedByPc], [IsDeleted])
                SELECT
                    company.[Id],
                    serviceAccount.[Code],
                    serviceAccount.[Name],
                    parentAccount.[Id],
                    serviceAccount.[AccountType],
                    serviceAccount.[NormalBalance],
                    1,
                    1,
                    N'migration',
                    SYSUTCDATETIME(),
                    N'migration',
                    0
                FROM [Companies] AS company
                CROSS JOIN (VALUES
                    (N'4150', N'إيرادات الخدمات', N'4000', 4, 2),
                    (N'5250', N'مصروفات الخدمات', N'5000', 5, 1)
                ) AS serviceAccount
                    ([Code], [Name], [ParentCode], [AccountType], [NormalBalance])
                INNER JOIN [Accounts] AS parentAccount
                    ON parentAccount.[CompanyId] = company.[Id]
                    AND parentAccount.[Code] = serviceAccount.[ParentCode]
                    AND parentAccount.[IsActive] = 1
                    AND parentAccount.[IsDeleted] = 0
                WHERE company.[IsDeleted] = 0
                    AND NOT EXISTS (
                        SELECT 1
                        FROM [Accounts] AS existing
                        WHERE existing.[CompanyId] = company.[Id]
                            AND existing.[Code] = serviceAccount.[Code]
                            AND existing.[IsDeleted] = 0);
                """);

            migrationBuilder.Sql(
                """
                UPDATE serviceMapping
                SET serviceMapping.[AccountId] = dedicatedAccount.[Id],
                    serviceMapping.[UpdatedById] = N'migration',
                    serviceMapping.[UpdatedOn] = SYSUTCDATETIME(),
                    serviceMapping.[UpdatedByPc] = N'migration'
                FROM [AccountMappings] AS serviceMapping
                INNER JOIN [Accounts] AS currentAccount
                    ON currentAccount.[CompanyId] = serviceMapping.[CompanyId]
                    AND currentAccount.[Id] = serviceMapping.[AccountId]
                    AND currentAccount.[IsDeleted] = 0
                INNER JOIN [Accounts] AS dedicatedAccount
                    ON dedicatedAccount.[CompanyId] = serviceMapping.[CompanyId]
                    AND dedicatedAccount.[Code] = CASE
                        WHEN serviceMapping.[MappingType] IN (20, 21)
                            THEN N'4150'
                        ELSE N'5250'
                    END
                    AND dedicatedAccount.[AccountType] = CASE
                        WHEN serviceMapping.[MappingType] IN (20, 21)
                            THEN 4
                        ELSE 5
                    END
                    AND dedicatedAccount.[NormalBalance] = CASE
                        WHEN serviceMapping.[MappingType] IN (20, 21)
                            THEN 2
                        ELSE 1
                    END
                    AND dedicatedAccount.[IsPosting] = 1
                    AND dedicatedAccount.[IsActive] = 1
                    AND dedicatedAccount.[IsDeleted] = 0
                WHERE serviceMapping.[MappingType] IN (20, 21, 22, 23)
                    AND serviceMapping.[SourceId] IS NULL
                    AND serviceMapping.[IsDeleted] = 0
                    AND ((serviceMapping.[MappingType] IN (20, 21)
                            AND currentAccount.[Code] = N'4200')
                        OR (serviceMapping.[MappingType] IN (22, 23)
                            AND currentAccount.[Code] = N'5200'));

                INSERT INTO [AccountMappings]
                    ([CompanyId], [FiscalYearId], [MappingType], [SourceId],
                     [AccountId], [CreatedById], [CreatedOn], [CreatedByPc],
                     [IsDeleted])
                SELECT
                    fiscalYear.[CompanyId],
                    fiscalYear.[Id],
                    serviceMapping.[MappingType],
                    NULL,
                    account.[Id],
                    N'migration',
                    SYSUTCDATETIME(),
                    N'migration',
                    0
                FROM [FiscalYears] AS fiscalYear
                CROSS JOIN (VALUES
                    (20, N'4150'),
                    (21, N'4150'),
                    (22, N'5250'),
                    (23, N'5250')
                ) AS serviceMapping ([MappingType], [AccountCode])
                INNER JOIN [Accounts] AS account
                    ON account.[CompanyId] = fiscalYear.[CompanyId]
                    AND account.[Code] = serviceMapping.[AccountCode]
                    AND account.[AccountType] = CASE
                        WHEN serviceMapping.[MappingType] IN (20, 21)
                            THEN 4
                        ELSE 5
                    END
                    AND account.[NormalBalance] = CASE
                        WHEN serviceMapping.[MappingType] IN (20, 21)
                            THEN 2
                        ELSE 1
                    END
                    AND account.[IsPosting] = 1
                    AND account.[IsActive] = 1
                    AND account.[IsDeleted] = 0
                WHERE fiscalYear.[IsDeleted] = 0
                    AND NOT EXISTS (
                        SELECT 1
                        FROM [AccountMappings] AS existing
                        WHERE existing.[CompanyId] = fiscalYear.[CompanyId]
                            AND existing.[FiscalYearId] = fiscalYear.[Id]
                            AND existing.[MappingType] = serviceMapping.[MappingType]
                            AND existing.[SourceId] IS NULL
                            AND existing.[IsDeleted] = 0);
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO [AccountStatementMappings]
                    ([CompanyId], [FiscalYearId], [StatementType], [AccountId],
                     [FinancialStatementLineId], [CreatedById], [CreatedOn],
                     [CreatedByPc], [IsDeleted])
                SELECT
                    fiscalYear.[CompanyId],
                    fiscalYear.[Id],
                    statementTarget.[StatementType],
                    account.[Id],
                    statementLine.[Id],
                    N'migration',
                    SYSUTCDATETIME(),
                    N'migration',
                    0
                FROM [FiscalYears] AS fiscalYear
                CROSS JOIN (VALUES
                    (N'4150', 2, N'IS-110'),
                    (N'4150', 3, N'CF-110'),
                    (N'5250', 2, N'IS-220'),
                    (N'5250', 3, N'CF-130')
                ) AS statementTarget ([AccountCode], [StatementType], [LineCode])
                INNER JOIN [Accounts] AS account
                    ON account.[CompanyId] = fiscalYear.[CompanyId]
                    AND account.[Code] = statementTarget.[AccountCode]
                    AND account.[AccountType] = CASE
                        WHEN statementTarget.[AccountCode] = N'4150' THEN 4
                        ELSE 5
                    END
                    AND account.[NormalBalance] = CASE
                        WHEN statementTarget.[AccountCode] = N'4150' THEN 2
                        ELSE 1
                    END
                    AND account.[IsPosting] = 1
                    AND account.[IsActive] = 1
                    AND account.[IsDeleted] = 0
                INNER JOIN [FinancialStatementLines] AS statementLine
                    ON statementLine.[CompanyId] = fiscalYear.[CompanyId]
                    AND statementLine.[FiscalYearId] = fiscalYear.[Id]
                    AND statementLine.[StatementType] = statementTarget.[StatementType]
                    AND statementLine.[Code] = statementTarget.[LineCode]
                    AND statementLine.[IsDeleted] = 0
                WHERE fiscalYear.[IsDeleted] = 0
                    AND NOT EXISTS (
                        SELECT 1
                        FROM [AccountStatementMappings] AS existing
                        WHERE existing.[CompanyId] = fiscalYear.[CompanyId]
                            AND existing.[FiscalYearId] = fiscalYear.[Id]
                            AND existing.[StatementType] = statementTarget.[StatementType]
                            AND existing.[AccountId] = account.[Id]
                            AND existing.[IsDeleted] = 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM [AccountMappings] WHERE [MappingType] IN (20, 21, 22, 23);");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountMappings_MappingType",
                table: "AccountMappings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountMappings_MappingType",
                table: "AccountMappings",
                sql: "[MappingType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19)");
        }
    }
}
