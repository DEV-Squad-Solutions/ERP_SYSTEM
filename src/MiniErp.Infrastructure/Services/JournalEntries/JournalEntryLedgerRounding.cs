using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.JournalEntries;

/// <summary>
/// Rounds automatic journal lines to the persisted ledger scale
/// (JournalEntryLine.Debit/Credit are decimal(19,4)) so the balance is
/// validated on the exact values the database stores. Source documents keep
/// base amounts at <c>ExchangeRateRules.BaseAmountScale</c> (8), and rounding
/// each line independently can leave a residual of a few 0.0001 units; that
/// residual is placed on one line so the stored entry stays balanced.
/// </summary>
internal static class JournalEntryLedgerRounding
{
    public const int LedgerScale = 4;

    public static IReadOnlyList<JournalEntryLineRequest> RoundToLedgerScale(
        IReadOnlyList<JournalEntryLineRequest> lines,
        CurrencyCode baseCurrency)
    {
        var rounded = lines
            .Select(line => AlignBaseCurrencyTransaction(
                line with
                {
                    Debit = Round(line.Debit),
                    Credit = Round(line.Credit)
                },
                baseCurrency))
            .ToList();

        var residual = rounded.Sum(line => line.Debit) -
            rounded.Sum(line => line.Credit);
        if (residual == 0m)
        {
            return rounded;
        }

        var residualIndex = FindResidualLineIndex(
            rounded,
            baseCurrency,
            Math.Abs(residual));
        if (residualIndex < 0)
        {
            // Leave the imbalance for ValidateBalance to reject.
            return rounded;
        }

        var target = rounded[residualIndex];
        rounded[residualIndex] = AlignBaseCurrencyTransaction(
            target.Debit > 0m
                ? target with { Debit = target.Debit - residual }
                : target with { Credit = target.Credit + residual },
            baseCurrency);

        return rounded;
    }

    /// <summary>
    /// Prefers the largest base-currency line (for example the exchange
    /// gain/loss line), because its transaction amount can move with the
    /// ledger amount. A foreign-currency line is used only when no
    /// base-currency line exists.
    /// </summary>
    private static int FindResidualLineIndex(
        IReadOnlyList<JournalEntryLineRequest> lines,
        CurrencyCode baseCurrency,
        decimal absoluteResidual)
    {
        var candidates = lines
            .Select((line, index) => (
                Index: index,
                Amount: Math.Max(line.Debit, line.Credit),
                IsBaseCurrency: line.Currency is null ||
                    line.Currency == baseCurrency))
            .Where(candidate => candidate.Amount > absoluteResidual)
            .OrderByDescending(candidate => candidate.IsBaseCurrency)
            .ThenByDescending(candidate => candidate.Amount)
            .ThenBy(candidate => candidate.Index)
            .ToArray();

        return candidates.Length == 0 ? -1 : candidates[0].Index;
    }

    private static JournalEntryLineRequest AlignBaseCurrencyTransaction(
        JournalEntryLineRequest line,
        CurrencyCode baseCurrency) =>
        line.Currency == baseCurrency
            ? line with
            {
                TransactionDebit = line.Debit,
                TransactionCredit = line.Credit
            }
            : line;

    private static decimal Round(decimal value) =>
        decimal.Round(value, LedgerScale, MidpointRounding.AwayFromZero);
}
