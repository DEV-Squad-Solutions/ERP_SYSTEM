using FluentValidation;
using MiniErp.Domain.Entities.Inventory;

namespace MiniErp.Application.Features.InvoiceItemPricing;

public sealed class InvoiceItemPricingFilterRequestValidator
    : AbstractValidator<InvoiceItemPricingFilterRequest>
{
    public InvoiceItemPricingFilterRequestValidator()
    {
        RuleFor(request => request.Search)
            .MaximumLength(200);

        RuleFor(request => request.InvoiceId)
            .GreaterThan(0)
            .When(request => request.InvoiceId.HasValue);

        RuleFor(request => request.ItemId)
            .GreaterThan(0)
            .When(request => request.ItemId.HasValue);

        RuleFor(request => request.InvoiceType)
            .IsInEnum()
            .When(request => request.InvoiceType.HasValue);

        RuleFor(request => request)
            .Must(request =>
                !request.FromDate.HasValue ||
                !request.ToDate.HasValue ||
                request.ToDate.Value >= request.FromDate.Value)
            .WithMessage(
                "يجب أن يكون تاريخ النهاية مساويًا لتاريخ البداية أو بعده.");
    }
}

public sealed class ItemPricingExpenseRequestValidator
    : AbstractValidator<ItemPricingExpenseRequest>
{
    public ItemPricingExpenseRequestValidator()
    {
        RuleFor(expense => expense.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(expense => expense.Amount)
            .GreaterThan(0)
            .PrecisionScale(
                InventoryCostRules.ValuePrecision,
                InventoryCostRules.ValueScale,
                ignoreTrailingZeros: true);

        RuleFor(expense => expense.Notes)
            .MaximumLength(1_000);
    }
}

public sealed class ReplaceItemPricingExpensesRequestValidator
    : AbstractValidator<ReplaceItemPricingExpensesRequest>
{
    public ReplaceItemPricingExpensesRequestValidator()
    {
        RuleFor(request => request.Expenses)
            .NotNull()
            .Must(expenses => expenses is { Count: <= 25 })
            .WithMessage("لا يمكن إضافة أكثر من 25 مصروفًا استرشاديًا للصنف.")
            .Must(HaveUniqueNames)
            .WithMessage("لا يمكن تكرار اسم المصروف داخل الصنف.");

        RuleForEach(request => request.Expenses!)
            .SetValidator(new ItemPricingExpenseRequestValidator());
    }

    private static bool HaveUniqueNames(
        IReadOnlyList<ItemPricingExpenseRequest>? expenses)
    {
        if (expenses is null)
        {
            return true;
        }

        return expenses
            .Select(expense => expense.Name?.Trim() ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == expenses.Count;
    }
}
