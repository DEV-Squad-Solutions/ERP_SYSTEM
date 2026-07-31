using FluentValidation;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Companies;

public sealed class CompanyRequestValidator : AbstractValidator<CompanyRequest>
{
    public CompanyRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= 200)
            .WithMessage("يجب ألا يتجاوز طول اسم الشركة 200 حرفًا.");

        RuleFor(request => request.Address)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= 500)
            .WithMessage("يجب ألا يتجاوز طول العنوان 500 حرفًا.");

        RuleFor(request => request.CommercialRegister)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= 50)
            .WithMessage("يجب ألا يتجاوز طول السجل التجاري 50 حرفًا.");

        RuleFor(request => request.TaxNumber)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= 50)
            .WithMessage("يجب ألا يتجاوز طول الرقم الضريبي 50 حرفًا.");

        RuleFor(request => request.ManagerName)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= 200)
            .WithMessage("يجب ألا يتجاوز طول اسم المدير 200 حرفًا.");
        RuleFor(request => request.StockBalanceCheckMode)
            .Must(mode => !mode.HasValue || Enum.IsDefined(mode.Value))
            .WithMessage("Stock balance check mode is invalid.");

        RuleFor(request => request.BaseCurrency)
            .Must(currency =>
                !currency.HasValue ||
                Enum.IsDefined(currency.Value))
            .WithMessage("عملة الشركة الأساسية غير صالحة.");
    }
}

public sealed class CompanyUpdateRequestValidator
    : AbstractValidator<CompanyUpdateRequest>
{
    public CompanyUpdateRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= 200)
            .WithMessage("Company name must not exceed 200 characters.");

        RuleFor(request => request.Address)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= 500)
            .WithMessage("Address must not exceed 500 characters.");

        RuleFor(request => request.CommercialRegister)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= 50)
            .WithMessage("Commercial register must not exceed 50 characters.");

        RuleFor(request => request.TaxNumber)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= 50)
            .WithMessage("Tax number must not exceed 50 characters.");

        RuleFor(request => request.ManagerName)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= 200)
            .WithMessage("Manager name must not exceed 200 characters.");

        RuleFor(request => request.StockBalanceCheckMode)
            .Must(mode => !mode.HasValue || Enum.IsDefined(mode.Value))
            .WithMessage("Stock balance check mode is invalid.");

        RuleFor(request => request.BaseCurrency)
            .Must(currency =>
                !currency.HasValue ||
                Enum.IsDefined(currency.Value))
            .WithMessage("Base currency is invalid.");

        RuleFor(request => request.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("The current company rowVersion is required.");
    }
}
