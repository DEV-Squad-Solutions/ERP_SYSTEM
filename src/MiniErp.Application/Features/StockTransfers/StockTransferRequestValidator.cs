using FluentValidation;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.StockTransfers;

public sealed class StockTransferLineRequestValidator
    : AbstractValidator<StockTransferLineRequest>
{
    public StockTransferLineRequestValidator()
    {
        RuleFor(line => line.ItemId).GreaterThan(0);
        RuleFor(line => line.Quantity)
            .GreaterThan(0)
            .PrecisionScale(
                InventoryQuantityRules.Precision,
                InventoryQuantityRules.Scale,
                ignoreTrailingZeros: true);
        RuleFor(line => line.Notes)
            .MaximumLength(StockTransferRequest.NotesMaximumLength);
    }
}

public sealed class StockTransferRequestValidator
    : AbstractValidator<StockTransferRequest>
{
    public StockTransferRequestValidator()
    {
        AddCommonRules();
        RuleFor(request => request.SourceStoreId).GreaterThan(0);
        RuleFor(request => request.DestinationStoreId)
            .GreaterThan(0)
            .NotEqual(request => request.SourceStoreId)
            .WithMessage("يجب اختيار مخزن وجهة مختلف عن مخزن المصدر.");
    }

    private void AddCommonRules()
    {
        RuleFor(request => request.TransferDate)
            .Must(date => date != default)
            .WithMessage("تاريخ التحويل مطلوب.");
        RuleFor(request => request.Notes)
            .MaximumLength(StockTransferRequest.NotesMaximumLength);
        RuleFor(request => request.Lines)
            .NotNull()
            .NotEmpty()
            .Must(lines => lines is not null &&
                lines.Count <= StockTransferRequest.MaximumLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور التحويل {StockTransferRequest.MaximumLineCount}.")
            .Must(lines => lines is not null &&
                lines.Select(line => line.ItemId).Distinct().Count() ==
                lines.Count)
            .WithMessage("لا يجوز تكرار الصنف في سطور التحويل.");
        RuleForEach(request => request.Lines)
            .SetValidator(new StockTransferLineRequestValidator());
    }
}

public sealed class StockTransferUpdateRequestValidator
    : AbstractValidator<StockTransferUpdateRequest>
{
    public StockTransferUpdateRequestValidator()
    {
        RuleFor(request => request.TransferDate)
            .Must(date => date != default)
            .WithMessage("تاريخ التحويل مطلوب.");
        RuleFor(request => request.Notes)
            .MaximumLength(StockTransferRequest.NotesMaximumLength);
        RuleFor(request => request.Lines)
            .NotNull()
            .NotEmpty()
            .Must(lines => lines is not null &&
                lines.Count <= StockTransferRequest.MaximumLineCount)
            .WithMessage(
                $"لا يجوز أن يتجاوز عدد سطور التحويل {StockTransferRequest.MaximumLineCount}.")
            .Must(lines => lines is not null &&
                lines.Select(line => line.ItemId).Distinct().Count() ==
                lines.Count)
            .WithMessage("لا يجوز تكرار الصنف في سطور التحويل.");
        RuleForEach(request => request.Lines)
            .SetValidator(new StockTransferLineRequestValidator());
        RuleFor(request => request.RowVersion)
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithMessage("أعد تحميل التحويل ثم حاول التعديل مرة أخرى.");
    }
}

public sealed class StockTransferFilterRequestValidator
    : AbstractValidator<StockTransferFilterRequest>
{
    public StockTransferFilterRequestValidator()
    {
        RuleFor(filter => filter.Search)
            .MaximumLength(StockTransferFilterRequest.SearchMaximumLength);
        RuleFor(filter => filter.SourceStoreId)
            .GreaterThan(0)
            .When(filter => filter.SourceStoreId.HasValue);
        RuleFor(filter => filter.DestinationStoreId)
            .GreaterThan(0)
            .When(filter => filter.DestinationStoreId.HasValue);
        RuleFor(filter => filter.ItemId)
            .GreaterThan(0)
            .When(filter => filter.ItemId.HasValue);
        RuleFor(filter => filter)
            .Must(filter => !filter.FromDate.HasValue ||
                !filter.ToDate.HasValue ||
                filter.FromDate <= filter.ToDate)
            .WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}
