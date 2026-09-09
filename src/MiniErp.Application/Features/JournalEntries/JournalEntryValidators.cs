using FluentValidation;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;

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
        RuleFor(request => request.EntryType)
            .IsInEnum()
            .Must(type => type is
                JournalEntryType.Manual or
                JournalEntryType.Adjustment or
                JournalEntryType.Opening)
            .WithMessage("لا يمكن إنشاء القيد التلقائي يدويًا.");
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

public sealed class JournalEntryUpdateRequestValidator
    : AbstractValidator<JournalEntryUpdateRequest>
{
    public JournalEntryUpdateRequestValidator()
    {
        RuleFor(request => request.FiscalYearId).GreaterThan(0);
        RuleFor(request => request.EntryDate).NotEqual(default(DateOnly));
        RuleFor(request => request.Description)
            .NotEmpty()
            .MaximumLength(JournalEntryRequest.DescriptionMaximumLength);
        RuleFor(request => request.Lines)
            .NotNull()
            .Must(lines => lines is { Count: >= 2 })
            .WithMessage("يجب أن يحتوي القيد على سطرين على الأقل.")
            .Must(lines => lines is not null &&
                lines.Sum(line => line.Debit) > 0m &&
                lines.Sum(line => line.Debit) == lines.Sum(line => line.Credit))
            .WithMessage("إجمالي المدين يجب أن يساوي إجمالي الدائن ويكون أكبر من صفر.");
        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("يجب إرسال إصدار القيد الحالي قبل تعديله.");
        RuleForEach(request => request.Lines)
            .SetValidator(new JournalEntryLineRequestValidator());
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
        RuleFor(line => line.PartyType)
            .IsInEnum()
            .When(line => line.PartyType.HasValue);
        RuleFor(line => line.PartyId)
            .GreaterThan(0)
            .When(line => line.PartyId.HasValue);
        RuleFor(line => line)
            .Must(line => line.PartyType.HasValue == line.PartyId.HasValue)
            .WithName(nameof(JournalEntryLineRequest.PartyId))
            .WithMessage("يجب إرسال نوع الطرف ورقم الطرف معًا.");
        RuleFor(line => line.Currency)
            .IsInEnum()
            .When(line => line.Currency.HasValue);
        RuleFor(line => line.ExchangeRate)
            .Must(rate => !rate.HasValue || ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("سعر الصرف يجب أن يكون أكبر من صفر وبالدقة المسموحة.");
        RuleFor(line => line.TransactionDebit)
            .GreaterThanOrEqualTo(0m)
            .PrecisionScale(19, 4, ignoreTrailingZeros: true)
            .When(line => line.TransactionDebit.HasValue);
        RuleFor(line => line.TransactionCredit)
            .GreaterThanOrEqualTo(0m)
            .PrecisionScale(19, 4, ignoreTrailingZeros: true)
            .When(line => line.TransactionCredit.HasValue);
        RuleFor(line => line)
            .Must(line =>
                (line.Debit > 0m && line.Credit == 0m) ||
                (line.Credit > 0m && line.Debit == 0m))
            .WithName("Amount")
            .WithMessage("يجب إدخال مبلغ مدين أو دائن فقط لكل سطر.");
    }
}
