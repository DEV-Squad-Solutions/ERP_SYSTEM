using FluentValidation;

namespace MiniErp.Application.Features.Stores;

public sealed class StoreRequestValidator : AbstractValidator<StoreRequest>
{
    public StoreRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .Must(code => !string.IsNullOrWhiteSpace(code))
            .MaximumLength(50);

        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .MaximumLength(200);

        RuleFor(request => request.Address)
            .MaximumLength(500);

        RuleFor(request => request.BusinessPartnerId)
            .NotNull()
            .GreaterThan(0)
            .When(request => request.IsContainerStore)
            .WithMessage(
                "يجب تحديد عميل أو مورد صحيح للمخزن المخصص للعبوات.");

        RuleFor(request => request.BusinessPartnerId)
            .Null()
            .When(request => !request.IsContainerStore)
            .WithMessage(
                "يجب عدم تحديد عميل أو مورد لمخزن المنتجات.");
    }
}
