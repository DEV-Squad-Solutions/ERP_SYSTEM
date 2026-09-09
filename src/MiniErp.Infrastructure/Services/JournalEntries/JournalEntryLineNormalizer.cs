using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using static MiniErp.Application.Features.JournalEntries.JournalEntryErrors;

namespace MiniErp.Infrastructure.Services.JournalEntries;

/// <summary>
/// Normalizes the optional transaction-currency metadata on journal lines.
/// Debit/Credit are always ledger amounts in the company's base currency.
/// </summary>
internal static class JournalEntryLineNormalizer
{
    public static Result<IReadOnlyList<JournalEntryLineRequest>> Normalize(
        IReadOnlyList<JournalEntryLineRequest> lines,
        CurrencyCode baseCurrency)
    {
        var normalized = new List<JournalEntryLineRequest>(lines.Count);
        var errors = new List<Error>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var suppliedCount =
                (line.Currency.HasValue ? 1 : 0) +
                (line.ExchangeRate.HasValue ? 1 : 0) +
                (line.TransactionDebit.HasValue ? 1 : 0) +
                (line.TransactionCredit.HasValue ? 1 : 0);

            if (suppliedCount == 0)
            {
                normalized.Add(line with
                {
                    Currency = baseCurrency,
                    ExchangeRate = 1m,
                    TransactionDebit = line.Debit,
                    TransactionCredit = line.Credit
                });
                continue;
            }

            if (suppliedCount != 4 || !line.Currency.HasValue ||
                !line.ExchangeRate.HasValue ||
                !line.TransactionDebit.HasValue ||
                !line.TransactionCredit.HasValue)
            {
                errors.Add(CurrencyMetadataIncomplete(index));
                continue;
            }

            var currency = line.Currency.Value;
            var rate = line.ExchangeRate.Value;
            var transactionDebit = line.TransactionDebit.Value;
            var transactionCredit = line.TransactionCredit.Value;

            if (!Enum.IsDefined(currency))
            {
                errors.Add(CurrencyInvalid(index));
            }

            if (!ExchangeRateRules.IsValidRate(rate))
            {
                errors.Add(ExchangeRateInvalid(index));
            }
            else if (currency == baseCurrency && rate != 1m)
            {
                errors.Add(BaseCurrencyRateMustBeOne(index));
            }

            if (transactionDebit < 0m || transactionCredit < 0m ||
                !FitsJournalPrecision(transactionDebit) ||
                !FitsJournalPrecision(transactionCredit) ||
                (transactionDebit > 0m && transactionCredit > 0m) ||
                (line.Debit > 0m && transactionDebit <= 0m) ||
                (line.Credit > 0m && transactionCredit <= 0m) ||
                (line.Debit > 0m && transactionCredit != 0m) ||
                (line.Credit > 0m && transactionDebit != 0m))
            {
                errors.Add(TransactionAmountShapeInvalid(index));
            }

            if (ExchangeRateRules.IsValidRate(rate) &&
                ((line.Debit > 0m &&
                    RoundJournal(transactionDebit * rate) != RoundJournal(line.Debit)) ||
                 (line.Credit > 0m &&
                    RoundJournal(transactionCredit * rate) != RoundJournal(line.Credit))))
            {
                errors.Add(TransactionConversionMismatch(index));
            }

            normalized.Add(line with
            {
                Currency = currency,
                ExchangeRate = ExchangeRateRules.RoundRate(rate),
                TransactionDebit = decimal.Round(transactionDebit, 4, MidpointRounding.AwayFromZero),
                TransactionCredit = decimal.Round(transactionCredit, 4, MidpointRounding.AwayFromZero)
            });
        }

        return errors.Count > 0
            ? Result<IReadOnlyList<JournalEntryLineRequest>>.Failure(errors)
            : Result<IReadOnlyList<JournalEntryLineRequest>>.Success(normalized);
    }

    private static bool FitsJournalPrecision(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero) == value &&
        Math.Abs(value) < 1_000_000_000_000_000m;

    private static decimal RoundJournal(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
