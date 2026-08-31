using FluentValidation;

namespace MiniErp.Application.Features.FinancialStatementLines;

public sealed class FinancialStatementLineRequestValidator
    : AbstractValidator<FinancialStatementLineRequest>
{
    public FinancialStatementLineRequestValidator()
    {
        RuleFor(request => request.FiscalYearId).GreaterThan(0);
        RuleFor(request => request.StatementType).IsInEnum();
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(FinancialStatementLineRequest.CodeMaximumLength);
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(FinancialStatementLineRequest.NameMaximumLength);
        RuleFor(request => request.ParentLineId)
            .GreaterThan(0)
            .When(request => request.ParentLineId.HasValue);
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class FinancialStatementLineUpdateRequestValidator
    : AbstractValidator<FinancialStatementLineUpdateRequest>
{
    public FinancialStatementLineUpdateRequestValidator()
    {
        RuleFor(request => request.FiscalYearId).GreaterThan(0);
        RuleFor(request => request.StatementType).IsInEnum();
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(FinancialStatementLineRequest.CodeMaximumLength);
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(FinancialStatementLineRequest.NameMaximumLength);
        RuleFor(request => request.ParentLineId)
            .GreaterThan(0)
            .When(request => request.ParentLineId.HasValue);
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("يجب إرسال إصدار بند القائمة الحالي للتعديل.");
    }
}

public sealed class FinancialStatementLineFilterRequestValidator
    : AbstractValidator<FinancialStatementLineFilterRequest>
{
    public FinancialStatementLineFilterRequestValidator()
    {
        RuleFor(request => request.FiscalYearId).GreaterThan(0);
        RuleFor(request => request.StatementType).IsInEnum();
        RuleFor(request => request.Search)
            .MaximumLength(FinancialStatementLineRequest.NameMaximumLength);
        RuleFor(request => request.ParentLineId)
            .GreaterThan(0)
            .When(request => request.ParentLineId.HasValue);
    }
}
