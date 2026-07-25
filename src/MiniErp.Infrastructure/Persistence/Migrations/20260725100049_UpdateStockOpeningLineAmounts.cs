using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStockOpeningLineAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ItemUnitId",
                table: "StockOpeningBalanceLines",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "Count",
                table: "StockOpeningBalanceLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "StockOpeningBalanceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "StockOpeningBalanceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "StockOpeningBalanceLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [StockOpeningBalanceLines]
                SET
                    [Count] = 1,
                    [Weight] = [Quantity],
                    [Price] = 0,
                    [Total] = 0;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Count",
                table: "StockOpeningBalanceLines",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "StockOpeningBalanceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Total",
                table: "StockOpeningBalanceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Weight",
                table: "StockOpeningBalanceLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockOpeningBalanceLines_Count_Positive",
                table: "StockOpeningBalanceLines",
                sql: "[Count] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockOpeningBalanceLines_Price_NonNegative",
                table: "StockOpeningBalanceLines",
                sql: "[Price] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockOpeningBalanceLines_Total_NonNegative",
                table: "StockOpeningBalanceLines",
                sql: "[Total] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockOpeningBalanceLines_Weight_Positive",
                table: "StockOpeningBalanceLines",
                sql: "[Weight] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockOpeningBalanceLines_Count_Positive",
                table: "StockOpeningBalanceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockOpeningBalanceLines_Price_NonNegative",
                table: "StockOpeningBalanceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockOpeningBalanceLines_Total_NonNegative",
                table: "StockOpeningBalanceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockOpeningBalanceLines_Weight_Positive",
                table: "StockOpeningBalanceLines");

            migrationBuilder.DropColumn(
                name: "Count",
                table: "StockOpeningBalanceLines");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "StockOpeningBalanceLines");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "StockOpeningBalanceLines");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "StockOpeningBalanceLines");

            migrationBuilder.Sql(
                """
                UPDATE [line]
                SET [line].[ItemUnitId] = [item].[ItemUnitId]
                FROM [StockOpeningBalanceLines] AS [line]
                INNER JOIN [Items] AS [item]
                    ON [line].[CompanyId] = [item].[CompanyId]
                    AND [line].[ItemId] = [item].[Id]
                WHERE [line].[ItemUnitId] IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ItemUnitId",
                table: "StockOpeningBalanceLines",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
