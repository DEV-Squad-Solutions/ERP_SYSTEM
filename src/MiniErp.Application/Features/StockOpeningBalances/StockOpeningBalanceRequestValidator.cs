using FluentValidation;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.StockOpeningBalances;

public sealed class StockOpeningBalanceLineRequestValidator
    : AbstractValidator<StockOpeningBalanceLineRequest>
{
    public StockOpeningBalanceLineRequestValidator()
    {
        RuleFor(line => line.ItemId)
            .GreaterThan(0);

        RuleFor(line => line.Count)
            .GreaterThan(0);

        RuleFor(line => line.Weight)
            .GreaterThan(0)
            .PrecisionScale(
                StockOpeningBalanceAmountRules.QuantityPrecision,
                StockOpeningBalanceAmountRules.QuantityScale,
                ignoreTrailingZeros: true);

        RuleFor(line => line.Price)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(
                StockOpeningBalanceAmountRules.MoneyPrecision,
                StockOpeningBalanceAmountRules.MoneyScale,
                ignoreTrailingZeros: true);

        RuleFor(line => line)
            .Must(line => StockOpeningBalanceAmountRules.TryCalculate(
                line.Count,
                line.Weight,
                line.Price,
                out _,
                out _))
            .WithMessage(
                "ناتج الكمية أو الإجمالي يتجاوز الدقة الرقمية المسموح بها.");

        RuleFor(line => line.Notes)
            .MaximumLength(StockOpeningBalanceRequest.NotesMaximumLength)
            .When(line =>
                line.Notes is not null &&
                line.Notes.Trim().Length >
                StockOpeningBalanceRequest.NotesMaximumLength);
    }
}

public sealed class StockOpeningBalanceRequestValidator
    : AbstractValidator<StockOpeningBalanceRequest>
{
    public StockOpeningBalanceRequestValidator()
    {
        RuleFor(request => request.StoreId)
            .GreaterThan(0);

        RuleFor(request => request.DocumentNumber)
            .NotEmpty();

        RuleFor(request => request.DocumentNumber)
            .MaximumLength(StockOpeningBalanceRequest.DocumentNumberMaximumLength)
            .When(request =>
                request.DocumentNumber is not null &&
                request.DocumentNumber.Trim().Length >
                StockOpeningBalanceRequest.DocumentNumberMaximumLength);

        RuleFor(request => request.DocumentDate)
            .Must(date => date != default)
            .WithMessage("تاريخ المستند مطلوب.");

        RuleFor(request => request.Lines)
            .NotNull()
            .NotEmpty()
            .Must(lines =>
                lines is not null &&
                lines.Count <= StockOpeningBalanceRequest.MaximumLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور الرصيد الافتتاحي {StockOpeningBalanceRequest.MaximumLineCount}.")
            .Must(lines =>
                lines is not null &&
                lines.All(line => line is not null))
            .WithMessage("كل سطر في المستند مطلوب.")
            .Must(lines =>
                lines is not null &&
                lines.All(line => line is not null) &&
                lines.Select(line => line.ItemId).Distinct().Count() ==
                lines.Count)
            .WithMessage("لا يجوز تكرار الصنف في سطور الرصيد الافتتاحي.");

        RuleFor(request => request.Notes)
            .MaximumLength(StockOpeningBalanceRequest.NotesMaximumLength)
            .When(request =>
                request.Notes is not null &&
                request.Notes.Trim().Length >
                StockOpeningBalanceRequest.NotesMaximumLength);

        RuleForEach(request => request.Lines)
            .SetValidator(new StockOpeningBalanceLineRequestValidator());
    }
}

public sealed class StockOpeningBalanceUpdateRequestValidator
    : AbstractValidator<StockOpeningBalanceUpdateRequest>
{
    public StockOpeningBalanceUpdateRequestValidator()
    {
        RuleFor(request => request.StoreId)
            .GreaterThan(0);

        RuleFor(request => request.DocumentNumber)
            .NotEmpty();

        RuleFor(request => request.DocumentNumber)
            .MaximumLength(StockOpeningBalanceRequest.DocumentNumberMaximumLength)
            .When(request =>
                request.DocumentNumber is not null &&
                request.DocumentNumber.Trim().Length >
                StockOpeningBalanceRequest.DocumentNumberMaximumLength);

        RuleFor(request => request.DocumentDate)
            .Must(date => date != default)
            .WithMessage("تاريخ المستند مطلوب.");

        RuleFor(request => request.Lines)
            .NotNull()
            .NotEmpty()
            .Must(lines =>
                lines is not null &&
                lines.Count <= StockOpeningBalanceRequest.MaximumLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور الرصيد الافتتاحي {StockOpeningBalanceRequest.MaximumLineCount}.")
            .Must(lines =>
                lines is not null &&
                lines.All(line => line is not null))
            .WithMessage("كل سطر في المستند مطلوب.")
            .Must(lines =>
                lines is not null &&
                lines.All(line => line is not null) &&
                lines.Select(line => line.ItemId).Distinct().Count() ==
                lines.Count)
            .WithMessage("لا يجوز تكرار الصنف في سطور الرصيد الافتتاحي.");

        RuleFor(request => request.Notes)
            .MaximumLength(StockOpeningBalanceRequest.NotesMaximumLength)
            .When(request =>
                request.Notes is not null &&
                request.Notes.Trim().Length >
                StockOpeningBalanceRequest.NotesMaximumLength);

        RuleForEach(request => request.Lines)
            .SetValidator(new StockOpeningBalanceLineRequestValidator());

        RuleFor(request => request.RowVersion)
            .Must(rowVersion => rowVersion is { Length: > 0 })
            .WithMessage("يجب إرسال إصدار السجل الحالي للتعديل.");
    }
}
