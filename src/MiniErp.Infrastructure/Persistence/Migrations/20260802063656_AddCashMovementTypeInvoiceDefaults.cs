using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashMovementTypeInvoiceDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultForPurchase",
                table: "CashMovementTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultForPurchaseReturn",
                table: "CashMovementTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultForSales",
                table: "CashMovementTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultForSalesReturn",
                table: "CashMovementTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashMovementTypes_InvoiceDefaults",
                table: "CashMovementTypes",
                sql: "(([IsDefaultForSales] = 0 AND [IsDefaultForPurchaseReturn] = 0) OR ([IsActive] = 1 AND [Direction] = 1 AND [PartnerEffect] = 2)) AND (([IsDefaultForPurchase] = 0 AND [IsDefaultForSalesReturn] = 0) OR ([IsActive] = 1 AND [Direction] = 2 AND [PartnerEffect] = 1))");

            migrationBuilder.Sql(
                """
                ;WITH RankedSales AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [CompanyId]
                               ORDER BY CASE WHEN [Name] = 'Customer Collection' THEN 0 ELSE 1 END,
                                        [Id]
                           ) AS [RowNumber]
                    FROM [CashMovementTypes]
                    WHERE [IsDeleted] = 0
                      AND [IsActive] = 1
                      AND [Direction] = 1
                      AND [PartnerEffect] = 2
                )
                UPDATE [CashMovementTypes]
                SET [IsDefaultForSales] = 1
                WHERE [Id] IN
                (
                    SELECT [Id]
                    FROM RankedSales
                    WHERE [RowNumber] = 1
                );

                ;WITH RankedPurchaseReturns AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [CompanyId]
                               ORDER BY CASE WHEN [Name] = 'Supplier Refund' THEN 0 ELSE 1 END,
                                        [Id]
                           ) AS [RowNumber]
                    FROM [CashMovementTypes]
                    WHERE [IsDeleted] = 0
                      AND [IsActive] = 1
                      AND [Direction] = 1
                      AND [PartnerEffect] = 2
                )
                UPDATE [CashMovementTypes]
                SET [IsDefaultForPurchaseReturn] = 1
                WHERE [Id] IN
                (
                    SELECT [Id]
                    FROM RankedPurchaseReturns
                    WHERE [RowNumber] = 1
                );

                ;WITH RankedPurchases AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [CompanyId]
                               ORDER BY CASE WHEN [Name] = 'Supplier Payment' THEN 0 ELSE 1 END,
                                        [Id]
                           ) AS [RowNumber]
                    FROM [CashMovementTypes]
                    WHERE [IsDeleted] = 0
                      AND [IsActive] = 1
                      AND [Direction] = 2
                      AND [PartnerEffect] = 1
                )
                UPDATE [CashMovementTypes]
                SET [IsDefaultForPurchase] = 1
                WHERE [Id] IN
                (
                    SELECT [Id]
                    FROM RankedPurchases
                    WHERE [RowNumber] = 1
                );

                ;WITH RankedSalesReturns AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [CompanyId]
                               ORDER BY CASE WHEN [Name] = 'Customer Refund' THEN 0 ELSE 1 END,
                                        [Id]
                           ) AS [RowNumber]
                    FROM [CashMovementTypes]
                    WHERE [IsDeleted] = 0
                      AND [IsActive] = 1
                      AND [Direction] = 2
                      AND [PartnerEffect] = 1
                )
                UPDATE [CashMovementTypes]
                SET [IsDefaultForSalesReturn] = 1
                WHERE [Id] IN
                (
                    SELECT [Id]
                    FROM RankedSalesReturns
                    WHERE [RowNumber] = 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementTypes_CompanyId_DefaultForPurchase",
                table: "CashMovementTypes",
                columns: new[] { "CompanyId", "IsDefaultForPurchase" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsDefaultForPurchase] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementTypes_CompanyId_DefaultForPurchaseReturn",
                table: "CashMovementTypes",
                columns: new[] { "CompanyId", "IsDefaultForPurchaseReturn" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsDefaultForPurchaseReturn] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementTypes_CompanyId_DefaultForSales",
                table: "CashMovementTypes",
                columns: new[] { "CompanyId", "IsDefaultForSales" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsDefaultForSales] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementTypes_CompanyId_DefaultForSalesReturn",
                table: "CashMovementTypes",
                columns: new[] { "CompanyId", "IsDefaultForSalesReturn" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsDefaultForSalesReturn] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashMovementTypes_CompanyId_DefaultForPurchase",
                table: "CashMovementTypes");

            migrationBuilder.DropIndex(
                name: "IX_CashMovementTypes_CompanyId_DefaultForPurchaseReturn",
                table: "CashMovementTypes");

            migrationBuilder.DropIndex(
                name: "IX_CashMovementTypes_CompanyId_DefaultForSales",
                table: "CashMovementTypes");

            migrationBuilder.DropIndex(
                name: "IX_CashMovementTypes_CompanyId_DefaultForSalesReturn",
                table: "CashMovementTypes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashMovementTypes_InvoiceDefaults",
                table: "CashMovementTypes");

            migrationBuilder.DropColumn(
                name: "IsDefaultForPurchase",
                table: "CashMovementTypes");

            migrationBuilder.DropColumn(
                name: "IsDefaultForPurchaseReturn",
                table: "CashMovementTypes");

            migrationBuilder.DropColumn(
                name: "IsDefaultForSales",
                table: "CashMovementTypes");

            migrationBuilder.DropColumn(
                name: "IsDefaultForSalesReturn",
                table: "CashMovementTypes");
        }
    }
}
