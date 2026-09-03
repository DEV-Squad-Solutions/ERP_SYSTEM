using FluentValidation;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Statements;

public sealed record FinancialStatementReportRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    int? FiscalYearId = null,
    TrialBalanceViewMode ViewMode = TrialBalanceViewMode.Detailed,
    TrialBalanceAdjustmentView AdjustmentView =
        TrialBalanceAdjustmentView.AfterAdjustments,
    bool IncludeUnmapped = true);

public sealed class FinancialStatementReportRequestValidator
    : AbstractValidator<FinancialStatementReportRequest>
{
    public FinancialStatementReportRequestValidator()
    {
        RuleFor(request => request.FromDate)
            .NotEqual(default(DateOnly));
        RuleFor(request => request.ToDate)
            .NotEqual(default(DateOnly))
            .GreaterThanOrEqualTo(request => request.FromDate)
            .WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
        RuleFor(request => request.FiscalYearId)
            .GreaterThan(0)
            .When(request => request.FiscalYearId.HasValue);
        RuleFor(request => request.ViewMode).IsInEnum();
        RuleFor(request => request.AdjustmentView).IsInEnum();
    }
}
