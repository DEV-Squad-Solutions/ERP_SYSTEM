using FluentValidation;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.JournalEntries;

public sealed record JournalEntryFilterRequest(
    string? Search = null,
    int? FiscalYearId = null,
    JournalEntryType? EntryType = null,
    JournalEntryStatus? Status = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);

public sealed class JournalEntryFilterRequestValidator
    : AbstractValidator<JournalEntryFilterRequest>
{
    public JournalEntryFilterRequestValidator()
    {
        RuleFor(filter => filter.Search)
            .MaximumLength(JournalEntryRequest.DescriptionMaximumLength);
        RuleFor(filter => filter.FiscalYearId)
            .GreaterThan(0)
            .When(filter => filter.FiscalYearId.HasValue);
        RuleFor(filter => filter.EntryType!.Value)
            .IsInEnum()
            .When(filter => filter.EntryType.HasValue);
        RuleFor(filter => filter.Status!.Value)
            .IsInEnum()
            .When(filter => filter.Status.HasValue);
        RuleFor(filter => filter.ToDate)
            .GreaterThanOrEqualTo(filter => filter.FromDate)
            .When(filter => filter.FromDate.HasValue && filter.ToDate.HasValue)
            .WithMessage("تاريخ النهاية يجب ألا يسبق تاريخ البداية.");
    }
}
