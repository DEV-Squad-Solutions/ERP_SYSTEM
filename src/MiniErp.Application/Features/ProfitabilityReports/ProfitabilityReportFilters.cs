using FluentValidation;

namespace MiniErp.Application.Features.ProfitabilityReports;

public sealed record ProfitabilityReportFilterRequest(
    bool IncludeReturns = true,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int? BusinessPartnerId = null,
    int? StoreId = null,
    int? ItemId = null,
    int? ItemsCategoryId = null,
    string? Search = null);

public sealed class ProfitabilityReportFilterRequestValidator
    : AbstractValidator<ProfitabilityReportFilterRequest>
{
    public ProfitabilityReportFilterRequestValidator()
    {
        RuleFor(request => request.BusinessPartnerId)
            .GreaterThan(0)
            .When(request => request.BusinessPartnerId.HasValue);

        RuleFor(request => request.StoreId)
            .GreaterThan(0)
            .When(request => request.StoreId.HasValue);

        RuleFor(request => request.ItemId)
            .GreaterThan(0)
            .When(request => request.ItemId.HasValue);

        RuleFor(request => request.ItemsCategoryId)
            .GreaterThan(0)
            .When(request => request.ItemsCategoryId.HasValue);

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
