using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Cashboxes;
using static MiniErp.Application.Features.CashboxTransfers.CashboxTransferErrors;

namespace MiniErp.Infrastructure.Services.CashboxTransfers;

public sealed partial class CashboxTransferService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IExchangeRateResolver exchangeRateResolver,
    TimeProvider timeProvider,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null,
    ICashboxTransferPostingService? cashboxTransferPostingService = null,
    ICashboxAccessService? cashboxAccessService = null)
    : ICashboxTransferService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;
}
