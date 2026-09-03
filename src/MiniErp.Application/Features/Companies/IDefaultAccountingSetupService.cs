namespace MiniErp.Application.Features.Companies;

public interface IDefaultAccountingSetupService
{
    Task InitializeCompanyAsync(
        int companyId,
        DateOnly effectiveDate,
        CancellationToken cancellationToken = default);

    Task EnsureFiscalYearAsync(
        int companyId,
        int fiscalYearId,
        CancellationToken cancellationToken = default);

    Task EnsureCashboxAsync(
        int companyId,
        int cashboxId,
        CancellationToken cancellationToken = default);

    Task EnsureCashMovementTypeAsync(
        int companyId,
        int cashMovementTypeId,
        CancellationToken cancellationToken = default);
}
