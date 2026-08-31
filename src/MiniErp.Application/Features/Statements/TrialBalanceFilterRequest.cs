using FluentValidation;

namespace MiniErp.Application.Features.Statements;

public enum TrialBalanceViewMode
{
    Detailed = 1,
    Summary = 2
}

public enum TrialBalanceAdjustmentView
{
    BeforeAdjustments = 1,
    AfterAdjustments = 2
}

public sealed record TrialBalanceFilterRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    int? FiscalYearId = null,
    TrialBalanceViewMode ViewMode = TrialBalanceViewMode.Detailed,
    TrialBalanceAdjustmentView AdjustmentView =
        TrialBalanceAdjustmentView.BeforeAdjustments,
    bool IncludeZeroBalances = false,
    bool IncludeUnclassified = true);

public sealed class TrialBalanceFilterRequestValidator
    : AbstractValidator<TrialBalanceFilterRequest>
{
    public TrialBalanceFilterRequestValidator()
    {
        RuleFor(filter => filter.FromDate)
            .NotEqual(default(DateOnly));
        RuleFor(filter => filter.ToDate)
            .NotEqual(default(DateOnly))
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
        RuleFor(filter => filter.FiscalYearId)
            .GreaterThan(0)
            .When(filter => filter.FiscalYearId.HasValue);
        RuleFor(filter => filter.ViewMode).IsInEnum();
        RuleFor(filter => filter.AdjustmentView).IsInEnum();
    }
}
