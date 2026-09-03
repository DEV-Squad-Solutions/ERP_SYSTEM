using Mapster;
using MiniErp.Domain.Entities.Accounting;

namespace MiniErp.Application.Features.Accounts;

public sealed class AccountMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<AccountRequest, Account>()
            .Ignore(account => account.Id)
            .Ignore(account => account.CompanyId)
            .Ignore(account => account.Company)
            .Ignore(account => account.ParentAccount)
            .Ignore(account => account.Children)
            .Ignore(account => account.StatementMappings)
            .Ignore(account => account.RowVersion)
            .Map(account => account.Code, request => request.Code == null ? string.Empty : request.Code.Trim())
            .Map(account => account.Name, request => request.Name.Trim());

        config.ForType<AccountUpdateRequest, Account>()
            .Ignore(account => account.Id)
            .Ignore(account => account.CompanyId)
            .Ignore(account => account.Company)
            .Ignore(account => account.ParentAccount)
            .Ignore(account => account.Children)
            .Ignore(account => account.StatementMappings)
            .Ignore(account => account.RowVersion)
            .Map(account => account.Code, request => request.Code.Trim())
            .Map(account => account.Name, request => request.Name.Trim());

        config.ForType<Account, AccountResponse>()
            .Map(response => response.ParentAccountCode,
                account => account.ParentAccount == null
                    ? null
                    : account.ParentAccount.Code)
            .Map(response => response.ParentAccountName,
                account => account.ParentAccount == null
                    ? null
                    : account.ParentAccount.Name);

        config.ForType<Account, AccountSelectResponse>();
    }
}
