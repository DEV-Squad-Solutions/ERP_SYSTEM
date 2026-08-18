using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashMovementClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CashMovementTypes_InvoiceDefaults",
                table: "CashMovementTypes");

            migrationBuilder.AddColumn<int>(
                name: "Classification",
                table: "CashMovementTypes",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [CashMovementTypes]
                SET [Classification] = CASE
                    WHEN [PartnerEffect] <> 0 THEN 1
                    WHEN [Name] IN (N'Other Receipt', N'Other Payment', N'Driver Advance') THEN 4
                    WHEN [Direction] = 2 THEN 2
                    WHEN [Direction] = 1 THEN 3
                    ELSE 4
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Classification",
                table: "CashMovementTypes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovementTypes_CompanyId_Classification_Direction_IsActive_Name_Id",
                table: "CashMovementTypes",
                columns: new[] { "CompanyId", "Classification", "Direction", "IsActive", "Name", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashMovementTypes_Classification",
                table: "CashMovementTypes",
                sql: "[Classification] IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashMovementTypes_InvoiceDefaults",
                table: "CashMovementTypes",
                sql: "(([IsDefaultForSales] = 0 AND [IsDefaultForPurchaseReturn] = 0) OR ([IsActive] = 1 AND [Direction] = 1 AND [Classification] = 1 AND [PartnerEffect] = 2)) AND (([IsDefaultForPurchase] = 0 AND [IsDefaultForSalesReturn] = 0) OR ([IsActive] = 1 AND [Direction] = 2 AND [Classification] = 1 AND [PartnerEffect] = 1))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashMovementTypes_PartnerSettlement",
                table: "CashMovementTypes",
                sql: "[Classification] <> 1 OR [PartnerEffect] <> 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashMovementTypes_CompanyId_Classification_Direction_IsActive_Name_Id",
                table: "CashMovementTypes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashMovementTypes_Classification",
                table: "CashMovementTypes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashMovementTypes_InvoiceDefaults",
                table: "CashMovementTypes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashMovementTypes_PartnerSettlement",
                table: "CashMovementTypes");

            migrationBuilder.DropColumn(
                name: "Classification",
                table: "CashMovementTypes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashMovementTypes_InvoiceDefaults",
                table: "CashMovementTypes",
                sql: "(([IsDefaultForSales] = 0 AND [IsDefaultForPurchaseReturn] = 0) OR ([IsActive] = 1 AND [Direction] = 1 AND [PartnerEffect] = 2)) AND (([IsDefaultForPurchase] = 0 AND [IsDefaultForSalesReturn] = 0) OR ([IsActive] = 1 AND [Direction] = 2 AND [PartnerEffect] = 1))");
        }
    }
}
