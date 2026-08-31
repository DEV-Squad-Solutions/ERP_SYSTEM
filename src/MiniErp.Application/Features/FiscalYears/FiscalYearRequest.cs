namespace MiniErp.Application.Features.FiscalYears;

public sealed record FiscalYearRequest(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent = true)
{
    public const int NameMaximumLength = 200;
}

public sealed record FiscalYearUpdateRequest(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent,
    byte[]? RowVersion);
