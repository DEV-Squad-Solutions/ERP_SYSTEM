using Mapster;
using MiniErp.Domain.Entities.Employees;

namespace MiniErp.Application.Features.EmployeeOpeningBalances;

public sealed class EmployeeOpeningBalanceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<EmployeeOpeningBalanceRequest, EmployeeOpeningBalance>()
            .Ignore(balance => balance.DocumentNumber)
            .Ignore(balance => balance.PayrollEntryId)
            .Ignore(balance => balance.PayrollEntry)
            .Ignore(balance => balance.ExchangeRateRecord)
            .Ignore(balance => balance.ExchangeRateId)
            .Ignore(balance => balance.ExchangeRate)
            .Ignore(balance => balance.BaseAmount)
            .Map(balance => balance.Notes, request => Normalize(request.Notes));

        config.ForType<EmployeeOpeningBalanceUpdateRequest, EmployeeOpeningBalance>()
            .Ignore(balance => balance.DocumentNumber)
            .Ignore(balance => balance.PayrollEntryId)
            .Ignore(balance => balance.PayrollEntry)
            .Ignore(balance => balance.RowVersion)
            .Ignore(balance => balance.ExchangeRateRecord)
            .Ignore(balance => balance.ExchangeRateId)
            .Ignore(balance => balance.ExchangeRate)
            .Ignore(balance => balance.BaseAmount)
            .Map(balance => balance.Notes, request => Normalize(request.Notes));

        config.ForType<EmployeeOpeningBalance, EmployeeOpeningBalanceResponse>()
            .Map(
                response => response.BaseCurrency,
                balance => balance.Company.Settings == null
                    ? Domain.Enums.CurrencyCode.EGP
                    : balance.Company.Settings.BaseCurrency)
            .Map(
                response => response.EmployeeName,
                balance => balance.Employee.Name)
            .Map(
                response => response.EmployeeCode,
                balance => balance.Employee.Code);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
