using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Enums;

namespace MiniErp.Tests.Accounting;

public sealed class JournalEntryValidatorTests
{
    [Fact]
    public void RequestValidator_AcceptsBalancedEntry()
    {
        var result = new JournalEntryRequestValidator().Validate(
            CreateRequest(100m, 100m));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RequestValidator_RejectsUnbalancedEntry()
    {
        var result = new JournalEntryRequestValidator().Validate(
            CreateRequest(100m, 90m));

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(JournalEntryRequest.Lines));
    }

    [Fact]
    public void RequestValidator_RejectsDebitAndCreditOnSameLine()
    {
        var request = CreateRequest(100m, 100m) with
        {
            Lines =
            [
                new JournalEntryLineRequest(
                    AccountId: 1,
                    Description: null,
                    Debit: 100m,
                    Credit: 100m),
                new JournalEntryLineRequest(
                    AccountId: 2,
                    Description: null,
                    Debit: 0m,
                    Credit: 100m)
            ]
        };

        var result = new JournalEntryRequestValidator().Validate(request);

        Assert.Contains(
            result.Errors,
            error => error.PropertyName.Contains(
                "Amount",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ReverseValidator_RequiresEightByteRowVersion()
    {
        var result = new JournalEntryReverseRequestValidator().Validate(
            new JournalEntryReverseRequest(
                ReversalDate: new DateOnly(2026, 8, 31),
                Description: null,
                RowVersion: [1]));

        Assert.Contains(
            result.Errors,
            error => error.PropertyName ==
                nameof(JournalEntryReverseRequest.RowVersion));
    }

    private static JournalEntryRequest CreateRequest(
        decimal debit,
        decimal credit) =>
        new(
            FiscalYearId: 1,
            EntryDate: new DateOnly(2026, 8, 31),
            Description: "قيد اختبار",
            EntryType: JournalEntryType.Manual,
            Lines:
            [
                new JournalEntryLineRequest(
                    AccountId: 1,
                    Description: null,
                    Debit: debit,
                    Credit: 0m),
                new JournalEntryLineRequest(
                    AccountId: 2,
                    Description: null,
                    Debit: 0m,
                    Credit: credit)
            ]);
}
