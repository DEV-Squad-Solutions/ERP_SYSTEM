using FluentValidation;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeTransactions;

public sealed class EmployeeAccountEntryRequestValidator : AbstractValidator<EmployeeAccountEntryRequest>
{
    public EmployeeAccountEntryRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0)
            .WithMessage("معرف الموظف غير صحيح.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("نوع المعاملة غير صحيح.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("المبلغ يجب أن يكون أكبر من الصفر.");

        RuleFor(x => x.CashboxId)
            .GreaterThan(0)
            .WithMessage("يجب تحديد الصندوق النقدي.");

        RuleFor(x => x.CashMovementTypeId)
            .GreaterThan(0)
            .WithMessage("يجب تحديد نوع الحركة النقدية.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("الملاحظات يجب ألا تتجاوز 1000 حرف.");
    }
}

public sealed class IndividualEmployeeAccountEntryRequestValidator : AbstractValidator<IndividualEmployeeAccountEntryRequest>
{
    public IndividualEmployeeAccountEntryRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0)
            .WithMessage("معرف الموظف غير صحيح.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("نوع المعاملة غير صحيح.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("المبلغ يجب أن يكون أكبر من الصفر.");

        RuleFor(x => x.CashboxId)
            .GreaterThan(0)
            .When(x => x.CashboxId.HasValue)
            .WithMessage("معرف الصندوق النقدي غير صحيح.");

        RuleFor(x => x.CashMovementTypeId)
            .GreaterThan(0)
            .When(x => x.CashMovementTypeId.HasValue)
            .WithMessage("معرف نوع الحركة النقدية غير صحيح.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("الملاحظات يجب ألا تتجاوز 1000 حرف.");
    }
}

public sealed class BulkEmployeeAccountEntryRequestValidator : AbstractValidator<BulkEmployeeAccountEntryRequest>
{
    public const int MaxBatchSize = 1000;

    public BulkEmployeeAccountEntryRequestValidator()
    {
        RuleFor(x => x.Entries)
            .NotEmpty()
            .WithMessage("يجب إرسال معاملة واحدة على الأقل.")
            .Must(entries => entries.Count <= MaxBatchSize)
            .WithMessage($"لا يمكن إرسال أكثر من {MaxBatchSize} معاملة في الطلب الواحد.");

        RuleForEach(x => x.Entries)
            .SetValidator(new IndividualEmployeeAccountEntryRequestValidator());
    }
}

public sealed class EmployeeWithdrawalRequestValidator : AbstractValidator<EmployeeWithdrawalRequest>
{
    public EmployeeWithdrawalRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0)
            .WithMessage("معرف الموظف غير صحيح.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("مبلغ السحب يجب أن يكون أكبر من الصفر.");

        RuleFor(x => x.CashboxId)
            .GreaterThan(0)
            .WithMessage("يجب تحديد الصندوق النقدي للسحب.");

        RuleFor(x => x.CashMovementTypeId)
            .GreaterThan(0)
            .WithMessage("يجب تحديد نوع الحركة النقدية للسحب.");

        RuleFor(x => x.Type)
            .Must(type => type is EmployeeTransactionType.Withdrawal or EmployeeTransactionType.Advance)
            .WithMessage("نوع المعاملة يجب أن يكون سحب نقدي أو سلفة.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("الملاحظات يجب ألا تتجاوز 1000 حرف.");
    }
}

public sealed class IndividualEmployeeWithdrawalRequestValidator : AbstractValidator<IndividualEmployeeWithdrawalRequest>
{
    public IndividualEmployeeWithdrawalRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0)
            .WithMessage("معرف الموظف غير صحيح.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("مبلغ السحب يجب أن يكون أكبر من الصفر.");

        RuleFor(x => x.Type)
            .Must(type => type is EmployeeTransactionType.Withdrawal or EmployeeTransactionType.Advance)
            .WithMessage("نوع المعاملة يجب أن يكون سحب نقدي أو سلفة.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("الملاحظات يجب ألا تتجاوز 1000 حرف.");
    }
}

public sealed class BulkEmployeeWithdrawalRequestValidator : AbstractValidator<BulkEmployeeWithdrawalRequest>
{
    public const int MaxBatchSize = 1000;

    public BulkEmployeeWithdrawalRequestValidator()
    {
        RuleFor(x => x.Entries)
            .NotEmpty()
            .WithMessage("يجب إرسال معاملة سحب واحدة على الأقل.")
            .Must(entries => entries.Count <= MaxBatchSize)
            .WithMessage($"لا يمكن إرسال أكثر من {MaxBatchSize} معاملة سحب في الطلب الواحد.");

        RuleForEach(x => x.Entries)
            .SetValidator(new IndividualEmployeeWithdrawalRequestValidator());
    }
}

public sealed class EmployeeTransactionUpdateRequestValidator : AbstractValidator<EmployeeTransactionUpdateRequest>
{
    public EmployeeTransactionUpdateRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("المبلغ يجب أن يكون أكبر من الصفر.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("الملاحظات يجب ألا تتجاوز 1000 حرف.");
    }
}
