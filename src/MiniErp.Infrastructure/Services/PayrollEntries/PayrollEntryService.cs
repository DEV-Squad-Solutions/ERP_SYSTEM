using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.PayrollEntries
{
    public sealed partial class PayrollEntryService(
        ApplicationDbContext dbContext,
        IPaginationService paginationService,
        ICurrentCompanyContext currentCompanyContext,
        IExchangeRateResolver exchangeRateResolver,
        IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
        IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null)
        : IPayrollEntryService, IScopedService
    {
        private readonly int companyId = currentCompanyContext.CompanyId;

        // ─── GET ALL ────────────────────────────────────────────────────────────

        // ─── GET BY ID ──────────────────────────────────────────────────────────

        // ─── MOVE SALARY TO EMPLOYEE ACCOUNT ────────────────────────────────────

        // ─── MOVE SALARY BULK ────────────────────────────────────────────────────

        // ─── RECALCULATE ─────────────────────────────────────────────────────────

        // ─── DELETE ─────────────────────────────────────────────────────────────

        // ─── DELETE BULK ────────────────────────────────────────────────────────

        // ─── DASHBOARD ──────────────────────────────────────────────────────────

        // ─── ADD OUT COMPANY ───────────────────────────────────────────────────

        // ─── ADD OUT COMPANY BULK ───────────────────────────────────────────────

        // ─── UPDATE OUT COMPANY ────────────────────────────────────────────────

        // ─── UPDATE OUT COMPANY BULK ───────────────────────────────────────────

        // ─── MAPPING ────────────────────────────────────────────────────────────
    }
}
