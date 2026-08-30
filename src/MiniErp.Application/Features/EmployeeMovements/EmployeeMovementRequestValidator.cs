using FluentValidation;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeMovements;

public sealed class EmployeeMovementRequestValidator
    : AbstractValidator<EmployeeMovementRequest>
{
    public EmployeeMovementRequestValidator()
    {
        RuleFor(request => request.EmployeeId)
            .GreaterThan(0)
            .WithMessage("معرف الموظف غير صالح.");

        RuleFor(request => request.MovementDate)
            .Must(date => date != default)
            .WithMessage("تاريخ الحركة مطلوب.");

        RuleFor(request => request.Type)
            .IsInEnum()
            .WithMessage("نوع الحركة المحدد غير صالح.");

        RuleFor(request => request.Currency)
            .IsInEnum()
            .WithMessage("العملة المحددة غير صالحة.");

        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .PrecisionScale(
                PartnerOpeningBalanceAmountRules.MoneyPrecision,
                PartnerOpeningBalanceAmountRules.MoneyScale,
                ignoreTrailingZeros: true)
            .Must(PartnerOpeningBalanceAmountRules.IsValidAmount)
            .WithMessage("يجب أن يكون المبلغ موجباً وبحد أقصى منزلتين عشريتين.");

        RuleFor(request => request.ExchangeRate)
            .Must(rate => !rate.HasValue || ExchangeRateRules.IsValidRate(rate.Value))
            .WithMessage("يجب أن يكون سعر الصرف أكبر من صفر.");

        RuleFor(request => request.ExchangeRate)
            .NotNull()
            .GreaterThan(0)
            .When(request => request.Currency != CurrencyCode.EGP)
            .WithMessage("سعر الصرف مطلوب ويجب أن يكون أكبر من صفر للعملات الأجنبية.");

        RuleFor(request => request.CashboxId)
            .NotNull()
            .GreaterThan(0)
            .When(request => EmployeeAccountRules.RequiresCashVoucher(request.Type))
            .WithMessage("يجب تحديد الخزينة لصرف السلفة أو المسحوبات النقدية.");

        RuleFor(request => request.Notes)
            .MaximumLength(EmployeeMovementRequest.NotesMaximumLength)
            .When(request => !string.IsNullOrEmpty(request.Notes));
    }
}

public sealed class BulkEmployeeMovementRequestValidator
    : AbstractValidator<BulkEmployeeMovementRequest>
{
    public BulkEmployeeMovementRequestValidator()
    {
        RuleFor(request => request.Movements)
            .NotEmpty()
            .WithMessage("يجب إرسال حركة موظف واحدة على الأقل.");

        RuleForEach(request => request.Movements)
            .SetValidator(new EmployeeMovementRequestValidator());
    }
}

public sealed class EmployeeMovementFilterRequestValidator
    : AbstractValidator<EmployeeMovementFilterRequest>
{
    public EmployeeMovementFilterRequestValidator()
    {
        RuleFor(request => request.FromDate)
            .LessThanOrEqualTo(request => request.ToDate!.Value)
            .When(request => request.FromDate.HasValue && request.ToDate.HasValue)
            .WithMessage("تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.");

        RuleFor(request => request.Currency)
            .IsInEnum()
            .When(request => request.Currency.HasValue)
            .WithMessage("العملة المحددة غير صالحة.");

        RuleFor(request => request.Type)
            .IsInEnum()
            .When(request => request.Type.HasValue)
            .WithMessage("نوع الحركة المحدد غير صالح.");
    }
}
