using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Companies;

public sealed record CompanyRequest(
    string Name,
    string Address,
    string CommercialRegister,
    string TaxNumber,
    string ManagerName,
    StockBalanceCheckMode? StockBalanceCheckMode = null,
    CurrencyCode? BaseCurrency = null);

public sealed record CompanyUpdateRequest(
    string Name,
    string Address,
    string CommercialRegister,
    string TaxNumber,
    string ManagerName,
    StockBalanceCheckMode? StockBalanceCheckMode,
    CurrencyCode? BaseCurrency,
    byte[]? RowVersion);
