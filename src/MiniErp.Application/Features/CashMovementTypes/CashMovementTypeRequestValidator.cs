using FluentValidation;

namespace MiniErp.Application.Features.CashMovementTypes;

public sealed class CashMovementTypeRequestValidator
    : AbstractValidator<CashMovementTypeRequest>
{
    public CashMovementTypeRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(CashMovementTypeRequest.NameMaximumLength);

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.Notes)
            .MaximumLength(CashMovementTypeRequest.NotesMaximumLength);
    }
}

public sealed class CashMovementTypeUpdateRequestValidator
    : AbstractValidator<CashMovementTypeUpdateRequest>
{
    public CashMovementTypeUpdateRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(CashMovementTypeRequest.NameMaximumLength);

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.Notes)
            .MaximumLength(CashMovementTypeRequest.NotesMaximumLength);

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage(
                "يجب إرسال إصدار نوع الحركة النقدية الحالي للتعديل.");
    }
}
