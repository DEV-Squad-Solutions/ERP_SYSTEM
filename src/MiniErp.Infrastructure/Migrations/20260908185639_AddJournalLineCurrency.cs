using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalLineCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Currency",
                table: "JournalEntryLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "JournalEntryLines",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionCredit",
                table: "JournalEntryLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionDebit",
                table: "JournalEntryLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE lines " +
                "SET [Currency] = ISNULL(settings.[BaseCurrency], 1), " +
                "[ExchangeRate] = 1, " +
                "[TransactionDebit] = [Debit], " +
                "[TransactionCredit] = [Credit] " +
                "FROM [JournalEntryLines] AS lines " +
                "LEFT JOIN [CompanySettings] AS settings " +
                "ON settings.[CompanyId] = lines.[CompanyId]");

            migrationBuilder.AlterColumn<int>(
                name: "Currency",
                table: "JournalEntryLines",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "JournalEntryLines",
                type: "decimal(28,12)",
                precision: 28,
                scale: 12,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,12)",
                oldPrecision: 28,
                oldScale: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TransactionCredit",
                table: "JournalEntryLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TransactionDebit",
                table: "JournalEntryLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntryLines_Currency",
                table: "JournalEntryLines",
                sql: "[Currency] BETWEEN 1 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntryLines_ExchangeRate",
                table: "JournalEntryLines",
                sql: "[ExchangeRate] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntryLines_TransactionAmounts",
                table: "JournalEntryLines",
                sql: "(([TransactionDebit] > 0 AND [TransactionCredit] = 0 AND [Debit] > 0 AND [Credit] = 0) OR ([TransactionCredit] > 0 AND [TransactionDebit] = 0 AND [Credit] > 0 AND [Debit] = 0))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntryLines_Currency",
                table: "JournalEntryLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntryLines_ExchangeRate",
                table: "JournalEntryLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntryLines_TransactionAmounts",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "TransactionCredit",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "TransactionDebit",
                table: "JournalEntryLines");
        }
    }
}
