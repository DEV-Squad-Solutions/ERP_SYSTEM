using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.CashboxRevaluations;
using MiniErp.Infrastructure.Services.DriverOpeningBalances;
using static MiniErp.Application.Features.JournalEntries.JournalEntryErrors;

namespace MiniErp.Infrastructure.Services.JournalEntries;

public sealed partial class JournalEntryService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
    ICashboxAccessService? cashboxAccessService = null)
    : IJournalEntryService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;
}
