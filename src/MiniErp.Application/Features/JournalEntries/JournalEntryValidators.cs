using FluentValidation;

namespace MiniErp.Application.Features.JournalEntries;

public sealed class JournalEntryRequestValidator
    : AbstractValidator<JournalEntryRequest>
{
    public JournalEntryRequestValidator()
    {
        RuleFor(request => request.FiscalYearId).GreaterThan(0);
        RuleFor(request => request.EntryDate).NotEqual(default(DateOnly));
        RuleFor(request => request.Description)
            .NotEmpty()
            .MaximumLength(JournalEntryRequest.DescriptionMaximumLength);
        RuleFor(request => request.EntryType).IsInEnum();
        RuleFor(request => request.Lines)
            .NotNull()
            .Must(lines => lines is { Count: >= 2 })
            .WithMessage("يجب أن يحتوي القيد على سطرين على الأقل.")
            .Must(IsBalanced)
            .WithMessage("إجمالي المدين يجب أن يساوي إجمالي الدائن ويكون أكبر من صفر.");
        RuleForEach(request => request.Lines)
            .SetValidator(new JournalEntryLineRequestValidator());
    }

    private static bool IsBalanced(IReadOnlyList<JournalEntryLineRequest>? lines)
    {
        if (lines is null || lines.Count < 2)
        {
            return true;
        }

        var totalDebit = lines.Sum(line => line.Debit);
        var totalCredit = lines.Sum(line => line.Credit);
        return totalDebit > 0m && totalDebit == totalCredit;
    }
}

public sealed class JournalEntryLineRequestValidator
    : AbstractValidator<JournalEntryLineRequest>
{
    public JournalEntryLineRequestValidator()
    {
        RuleFor(line => line.AccountId).GreaterThan(0);
        RuleFor(line => line.Description)
            .MaximumLength(JournalEntryLineRequest.DescriptionMaximumLength);
        RuleFor(line => line.Debit)
            .GreaterThanOrEqualTo(0m)
            .PrecisionScale(19, 4, ignoreTrailingZeros: true);
        RuleFor(line => line.Credit)
            .GreaterThanOrEqualTo(0m)
            .PrecisionScale(19, 4, ignoreTrailingZeros: true);
        RuleFor(line => line)
            .Must(line =>
                (line.Debit > 0m && line.Credit == 0m) ||
                (line.Credit > 0m && line.Debit == 0m))
            .WithName("Amount")
            .WithMessage("يجب إدخال مبلغ مدين أو دائن فقط لكل سطر.");
    }
}

public sealed class JournalEntryReverseRequestValidator
    : AbstractValidator<JournalEntryReverseRequest>
{
    public JournalEntryReverseRequestValidator()
    {
        RuleFor(request => request.ReversalDate)
            .NotEqual(default(DateOnly));
        RuleFor(request => request.Description)
            .MaximumLength(JournalEntryRequest.DescriptionMaximumLength);
        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("يجب إرسال إصدار القيد الحالي قبل عكسه.");
    }
}
