using System.Data;
using System.Globalization;
using Mapster;
using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.FiscalYears.FiscalYearErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.AccountingReadiness;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.Companies;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.DriverOpeningBalances;
using MiniErp.Infrastructure.Services.ExchangeRates;

namespace MiniErp.Infrastructure.Services.FiscalYears;

public sealed partial class FiscalYearService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider,
    IAccountingReadinessService? accountingReadinessService = null,
    IDefaultAccountingSetupService? defaultAccountingSetupService = null,
    IFiscalYearInventoryCarryForwardService?
        inventoryCarryForwardService = null)
    : IFiscalYearService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;
}
