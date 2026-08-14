using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services;

public static class EntityIdentifierGenerator
{
    private const int MinimumDigits = 4;

    public static async Task<string> GenerateUniqueAsync(
        ApplicationDbContext dbContext,
        string prefix,
        int? companyId,
        IQueryable<string> existingIdentifiers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(existingIdentifiers);
        if (companyId is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(companyId),
                companyId,
                "Company identifiers must be positive when supplied.");
        }

        var normalizedPrefix = prefix.Trim().ToUpperInvariant();
        var scope = companyId.HasValue
            ? $"COMPANY:{companyId.Value.ToString(CultureInfo.InvariantCulture)}"
            : "GLOBAL";
        var number = await ReserveNumberAsync(
            dbContext,
            scope,
            normalizedPrefix,
            minimumNumber: 1,
            cancellationToken);
        var identifier = Create(normalizedPrefix, number);

        while (await existingIdentifiers.AnyAsync(
                   existing => existing == identifier,
                   cancellationToken))
        {
            number = await ReserveNumberAsync(
                dbContext,
                scope,
                normalizedPrefix,
                minimumNumber: checked(number + 1),
                cancellationToken);
            identifier = Create(normalizedPrefix, number);
        }

        return identifier;
    }

    public static string Create(
        string prefix,
        int number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number),
                number,
                "Identifier numbers must be positive.");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix.Trim().ToUpperInvariant()}-{number.ToString($"D{MinimumDigits}", CultureInfo.InvariantCulture)}");
    }

    private static async Task<int> ReserveNumberAsync(
        ApplicationDbContext dbContext,
        string scope,
        string prefix,
        int minimumNumber,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction
                ?.GetDbTransaction();
            command.CommandText = GetReservationSql(
                dbContext.Database.ProviderName);
            AddParameter(command, "@scope", scope);
            AddParameter(command, "@prefix", prefix);
            AddParameter(command, "@minimumNumber", minimumNumber);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string GetReservationSql(string? providerName)
    {
        if (providerName?.Contains(
                "Sqlite",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return """
                CREATE TABLE IF NOT EXISTS "EntityIdentifierSequences" (
                    "Scope" TEXT NOT NULL,
                    "Prefix" TEXT NOT NULL,
                    "LastNumber" INTEGER NOT NULL,
                    CONSTRAINT "PK_EntityIdentifierSequences"
                        PRIMARY KEY ("Scope", "Prefix")
                );

                INSERT INTO "EntityIdentifierSequences"
                    ("Scope", "Prefix", "LastNumber")
                VALUES (@scope, @prefix, @minimumNumber)
                ON CONFLICT ("Scope", "Prefix") DO UPDATE SET
                    "LastNumber" = CASE
                        WHEN "LastNumber" < excluded."LastNumber"
                            THEN excluded."LastNumber"
                        ELSE "LastNumber" + 1
                    END
                RETURNING "LastNumber";
                """;
        }

        return """
            MERGE [EntityIdentifierSequences] WITH (HOLDLOCK) AS target
            USING (VALUES (@scope, @prefix, @minimumNumber)) AS source
                ([Scope], [Prefix], [MinimumNumber])
            ON target.[Scope] = source.[Scope]
                AND target.[Prefix] = source.[Prefix]
            WHEN MATCHED THEN
                UPDATE SET [LastNumber] = CASE
                    WHEN target.[LastNumber] < source.[MinimumNumber]
                        THEN source.[MinimumNumber]
                    ELSE target.[LastNumber] + 1
                END
            WHEN NOT MATCHED THEN
                INSERT ([Scope], [Prefix], [LastNumber])
                VALUES (source.[Scope], source.[Prefix], source.[MinimumNumber])
            OUTPUT inserted.[LastNumber];
            """;
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
