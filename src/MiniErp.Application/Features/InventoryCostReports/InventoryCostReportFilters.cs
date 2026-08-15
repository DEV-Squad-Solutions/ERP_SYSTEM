using FluentValidation;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.InventoryCostReports;

public sealed record InventoryCostReportFilterRequest(
    int? StoreId = null,
    int? ItemId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    ItemMovementType? MovementType = null,
    InventoryCostStatus? CostStatus = null,
    string? Search = null);

public sealed class InventoryCostReportFilterRequestValidator
    : AbstractValidator<InventoryCostReportFilterRequest>
{
    public InventoryCostReportFilterRequestValidator()
    {
        RuleFor(request => request.StoreId)
            .NotNull()
            .GreaterThan(0)
            .WithMessage("يجب اختيار مخزن صالح لتقرير متوسط التكلفة.");

        RuleFor(request => request.ItemId)
            .NotNull()
            .GreaterThan(0)
            .WithMessage("يجب اختيار صنف صالح لتقرير متوسط التكلفة.");

        RuleFor(request => request.MovementType)
            .IsInEnum()
            .When(request => request.MovementType.HasValue);

        RuleFor(request => request.CostStatus)
            .IsInEnum()
            .When(request => request.CostStatus.HasValue);

        RuleFor(request => request)
            .Must(request =>
                !request.FromDate.HasValue ||
                !request.ToDate.HasValue ||
                request.ToDate.Value >= request.FromDate.Value)
            .WithMessage(
                "يجب أن يكون تاريخ النهاية مساويًا لتاريخ البداية أو بعده.");

        RuleFor(request => request.Search)
            .MaximumLength(200);
    }
}
