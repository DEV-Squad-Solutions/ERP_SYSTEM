using FluentValidation;

namespace MiniErp.Application.Features.FiscalYears;

public sealed class FiscalYearRequestValidator
    : AbstractValidator<FiscalYearRequest>
{
    public FiscalYearRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(FiscalYearRequest.NameMaximumLength);

        RuleFor(request => request)
            .Must(request => request.StartDate < request.EndDate)
            .WithMessage("يجب أن يكون تاريخ بداية السنة المالية قبل تاريخ نهايتها.");
    }
}

public sealed class FiscalYearUpdateRequestValidator
    : AbstractValidator<FiscalYearUpdateRequest>
{
    public FiscalYearUpdateRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(FiscalYearRequest.NameMaximumLength);

        RuleFor(request => request)
            .Must(request => request.StartDate < request.EndDate)
            .WithMessage("يجب أن يكون تاريخ بداية السنة المالية قبل تاريخ نهايتها.");

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("يجب إرسال إصدار السنة المالية الحالي للتعديل.");
    }
}

public sealed class FiscalYearFilterRequestValidator
    : AbstractValidator<FiscalYearFilterRequest>
{
    public FiscalYearFilterRequestValidator()
    {
        RuleFor(filter => filter.Search)
            .MaximumLength(FiscalYearRequest.NameMaximumLength);

        RuleFor(filter => filter.Status)
            .IsInEnum()
            .When(filter => filter.Status.HasValue);
    }
}
