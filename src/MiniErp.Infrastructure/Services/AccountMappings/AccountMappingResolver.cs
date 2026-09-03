using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.AccountMappings.AccountMappingErrors;

namespace MiniErp.Infrastructure.Services.AccountMappings;

public sealed class AccountMappingResolver(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IAccountMappingResolver, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<int>> ResolveAsync(
        int fiscalYearId,
        AccountingMappingType mappingType,
        int? sourceId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mappingType))
        {
            return Result<int>.Failure(
                InvalidMappingType(mappingType, 0));
        }

        var requiresSource = mappingType is
            AccountingMappingType.Cashbox or
            AccountingMappingType.CashMovementType;
        if (requiresSource != sourceId.HasValue)
        {
            return Result<int>.Failure(InvalidResolverSource(mappingType));
        }

        var accountId = await dbContext.AccountMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId &&
                mapping.MappingType == mappingType &&
                mapping.SourceId == sourceId)
            .Select(mapping => (int?)mapping.AccountId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!accountId.HasValue)
        {
            return Result<int>.Failure(
                MappingNotFound(fiscalYearId, mappingType, sourceId));
        }

        var accountIsValid = await dbContext.Accounts
            .AsNoTracking()
            .AnyAsync(account =>
                account.CompanyId == companyId &&
                account.Id == accountId.Value &&
                account.IsActive &&
                account.IsPosting &&
                account.ParentAccountId.HasValue,
                cancellationToken);
        return accountIsValid
            ? Result<int>.Success(accountId.Value)
            : Result<int>.Failure(
                AccountNotPostingOrInactive(accountId.Value, 0));
    }
}
