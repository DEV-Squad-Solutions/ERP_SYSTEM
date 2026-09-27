using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Tests;

internal static class TestFiscalYearSchema
{
    private static readonly string[] TransactionTables =
    [
        "Invoices",
        "InvoicePayments",
        "CashVouchers",
        "CashboxTransfers",
        "CashboxRevaluations",
        "MonetaryAccountRevaluations",
        "PartnerOpeningBalances",
        "BusinessPartnerMovements",
        "EmployeeOpeningBalances",
        "EmployeeMovements",
        "EmployeeAttendances",
        "PayrollEntries",
        "DriverTrips",
        "StockOpeningBalances",
        "StockAdjustments",
        "StockTransfers",
        "InventoryCounts",
        "ItemMovements",
        "ContainerMovements",
        "ExchangeRates"
    ];

    public static async Task EnsureAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsSqlite())
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS FiscalYears (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                Status INTEGER NOT NULL,
                IsCurrent INTEGER NOT NULL,
                ClosedOn TEXT NULL,
                RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                CreatedById TEXT NOT NULL DEFAULT '',
                CreatedOn TEXT NOT NULL DEFAULT '2026-01-01',
                CreatedByPc TEXT NOT NULL DEFAULT '',
                UpdatedById TEXT NULL,
                UpdatedOn TEXT NULL,
                UpdatedByPc TEXT NULL,
                DeletedById TEXT NULL,
                DeletedOn TEXT NULL,
                DeletedByPc TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );
            """,
            cancellationToken);

        foreach (var table in TransactionTables)
        {
            if (!await TableExistsAsync(
                    context,
                    table,
                    cancellationToken) ||
                await ColumnExistsAsync(
                    context,
                    table,
                    "FiscalYearId",
                    cancellationToken))
            {
                continue;
            }

            await context.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE [{table}] ADD COLUMN FiscalYearId INTEGER NOT NULL DEFAULT 1;",
                cancellationToken);
        }

        if (await TableExistsAsync(
                context,
                "Companies",
                cancellationToken))
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO FiscalYears (
                    Id, CompanyId, Name, StartDate, EndDate, Status,
                    IsCurrent, RowVersion, CreatedById, CreatedOn,
                    CreatedByPc, IsDeleted)
                SELECT company.Id,
                       company.Id,
                       'Test fiscal year',
                       '1900-01-01',
                       '2100-12-31',
                       1,
                       1,
                       randomblob(8),
                       'test',
                       '2026-01-01',
                       'test',
                       0
                FROM Companies AS company
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM FiscalYears AS fiscalYear
                    WHERE fiscalYear.CompanyId = company.Id);
                """,
                cancellationToken);
        }

        if (await TableExistsAsync(
                context,
                "ExchangeRates",
                cancellationToken) &&
            await ColumnExistsAsync(
                context,
                "ExchangeRates",
                "FiscalYearId",
                cancellationToken) &&
            await ColumnExistsAsync(
                context,
                "ExchangeRates",
                "RateDate",
                cancellationToken))
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                UPDATE ExchangeRates
                SET FiscalYearId = (
                    SELECT fiscalYear.Id
                    FROM FiscalYears AS fiscalYear
                    WHERE fiscalYear.CompanyId = ExchangeRates.CompanyId
                      AND ExchangeRates.RateDate BETWEEN
                          fiscalYear.StartDate AND fiscalYear.EndDate
                    LIMIT 1)
                WHERE EXISTS (
                    SELECT 1
                    FROM FiscalYears AS fiscalYear
                    WHERE fiscalYear.CompanyId = ExchangeRates.CompanyId
                      AND ExchangeRates.RateDate BETWEEN
                          fiscalYear.StartDate AND fiscalYear.EndDate);
                """,
                cancellationToken);
        }
    }

    private static async Task<bool> TableExistsAsync(
        ApplicationDbContext context,
        string table,
        CancellationToken cancellationToken) =>
        await ExecuteScalarAsync(
            context,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;",
            table,
            cancellationToken) > 0;

    private static async Task<bool> ColumnExistsAsync(
        ApplicationDbContext context,
        string table,
        string column,
        CancellationToken cancellationToken) =>
        await ExecuteScalarAsync(
            context,
            $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $name;",
            column,
            cancellationToken) > 0;

    private static async Task<long> ExecuteScalarAsync(
        ApplicationDbContext context,
        string commandText,
        string name,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = name;
        command.Parameters.Add(parameter);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken));
    }
}
