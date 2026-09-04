using FluentValidation;

namespace MiniErp.Application.Features.Dashboard;

public sealed record DashboardFilterRequest(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);

public sealed class DashboardFilterRequestValidator
    : AbstractValidator<DashboardFilterRequest>
{
    public DashboardFilterRequestValidator()
    {
        RuleFor(request => request)
            .Must(request =>
                !request.FromDate.HasValue ||
                !request.ToDate.HasValue ||
                request.ToDate.Value >= request.FromDate.Value)
            .WithMessage("يجب أن يكون تاريخ النهاية مساويًا لتاريخ البداية أو بعده.");

        RuleFor(request => request)
            .Must(request =>
                !request.FromDate.HasValue ||
                !request.ToDate.HasValue ||
                request.ToDate.Value.DayNumber -
                request.FromDate.Value.DayNumber <= 365)
            .WithMessage("يجب ألا تزيد فترة لوحة التحكم على 366 يومًا.");
    }
}
