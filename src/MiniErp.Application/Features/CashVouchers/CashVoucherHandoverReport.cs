using FluentValidation;
using MiniErp.Application.Common.Models;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashVouchers;

public sealed record CashVoucherHandoverReportFilterRequest(
    int? CashboxId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    CashDirection? Direction = null,
    string? Search = null);

public sealed class CashVoucherHandoverReportFilterRequestValidator
    : AbstractValidator<CashVoucherHandoverReportFilterRequest>
{
    public const int SearchMaximumLength = 200;

    public CashVoucherHandoverReportFilterRequestValidator()
    {
        RuleFor(filter => filter.CashboxId)
            .GreaterThan(0)
            .When(filter => filter.CashboxId.HasValue);
        RuleFor(filter => filter.Direction)
            .IsInEnum()
            .When(filter => filter.Direction.HasValue);
        RuleFor(filter => filter.Search)
            .MaximumLength(SearchMaximumLength);
        RuleFor(filter => filter)
            .Must(filter =>
                !filter.FromDate.HasValue ||
                !filter.ToDate.HasValue ||
                filter.FromDate.Value <= filter.ToDate.Value)
            .WithMessage("تاريخ البداية يجب أن يسبق تاريخ النهاية.")
            .WithName(nameof(CashVoucherHandoverReportFilterRequest.FromDate));
    }
}

public sealed record CashVoucherHandoverReportItemResponse(
    int Id,
    string VoucherNumber,
    DateOnly VoucherDate,
    CashDirection Direction,
    int CashboxId,
    string CashboxName,
    decimal Amount,
    CurrencyCode Currency,
    string? Description,
    string? Notes,
    string CreatedById,
    DateTime CreatedOn);

public sealed record CashVoucherHandoverCurrencySummary(
    CurrencyCode Currency,
    decimal Receipt,
    decimal Payment,
    decimal Net,
    int Count);

public sealed record CashVoucherHandoverReportResponse(
    IReadOnlyList<CashVoucherHandoverReportItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<CashVoucherHandoverCurrencySummary> Summaries);
