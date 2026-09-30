using Azure.Core;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.Employees;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Services.Statements;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace MiniErp.Infrastructure.Services.Employees
{
    public sealed partial class EmployeeService
    {
        private static IQueryable<Employee> ApplyFilters(
            IQueryable<Employee> query,
            EmployeeFilterRequest filters)
        {
            var search = filters.Search?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(employee =>
                    employee.Code.Contains(search) ||
                    employee.Name.Contains(search) ||
                    employee.Email != null &&
                    employee.Email.Contains(search) ||
                    employee.PhoneNumber != null &&
                    employee.PhoneNumber.Contains(search) ||
                    employee.Address != null &&
                    employee.Address.Contains(search) ||
                    employee.Type.ToString().Contains(search) ||
                    employee.JobTitle != null &&
                    employee.JobTitle.Contains(search) ||
                    employee.PlaceName != null &&
                    employee.PlaceName.Contains(search)
                    );
            }
            var name = filters.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(employee =>
                    employee.Name.Contains(name));
            }

            var code = filters.Code?.Trim();
            if (!string.IsNullOrWhiteSpace(code))
            {
                query = query.Where(employee =>
                    employee.Code.Contains(code));
            }
            var jobTitle = filters.JobTitle?.Trim();
            if (!string.IsNullOrWhiteSpace(jobTitle))
            {
                query = query.Where(employee =>
                    employee.JobTitle != null &&
                    employee.JobTitle.Contains(jobTitle));
            }

            var placeName = filters.PlaceName?.Trim();
            if (!string.IsNullOrWhiteSpace(placeName))
            {
                query = query.Where(employee =>
                    employee.PlaceName != null &&
                    employee.PlaceName.Contains(placeName));
            }

            if (filters.MinSalary.HasValue)
            {
                query = query.Where(employee =>
                employee.MonthlySalary.HasValue && employee.MonthlySalary.Value >= filters.MinSalary.Value
                || employee.DailySalary.HasValue && employee.DailySalary.Value >= filters.MinSalary.Value);
            }

            if (filters.MaxSalary.HasValue)
            {
                query = query.Where(employee =>
                employee.MonthlySalary.HasValue && employee.MonthlySalary.Value <= filters.MaxSalary.Value
                || employee.DailySalary.HasValue && employee.DailySalary.Value <= filters.MaxSalary.Value);
            }
            if (filters.EmployeeType.HasValue)
            {
                query = query.Where(employee =>
                employee.Type == filters.EmployeeType.Value);
            }
            if (filters.WorkPlaceStatus.HasValue)
            {
                query = query.Where(employee =>
                employee.WorkPlaceStatus == filters.WorkPlaceStatus.Value);
            }
            if (filters.IsActive.HasValue)
            {
                query = query.Where(employee =>
                employee.IsActive == filters.IsActive.Value);
            }
            return query;
        }
        private static IQueryable<Employee> ApplyFilters(
            IQueryable<Employee> query,
            EmployeeSelectedFilterRequest filters)
        {
            if (filters.EmployeeType.HasValue)
            {
                query = query.Where(employee =>
                employee.Type == filters.EmployeeType.Value);
            }
            if (filters.WorkPlaceStatus.HasValue)
            {
                query = query.Where(employee =>
                employee.WorkPlaceStatus == filters.WorkPlaceStatus.Value);
            }
            if (filters.IsActive.HasValue)
            {
                query = query.Where(employee =>
                employee.IsActive == filters.IsActive.Value);
            }
            return query;
        }


        private static async Task<(int TotalCount, EmployeeSummaryResponse Summary)>
            GetSummaryAsync(IQueryable<Employee> query,
                CancellationToken cancellationToken)
        {
            var summary = await query
                .GroupBy(_ => 1)
                .Select(group => new
                {                    
                    TotalCount = group.Count(),
                    TotalMonthlyEmployees = group.Count(e => e.Type == EmployeeType.Monthly),
                    TotalDailyEmployees = group.Count(e => e.Type == EmployeeType.Daily),
                    TotalActiveEmployees = group.Count(e => e.IsActive),
                    TotalInactiveEmployees = group.Count(e => !e.IsActive),
                    TotalInCompanyEmployees = group.Count(e => e.WorkPlaceStatus == WorkPlaceStatus.InCompany),
                    TotalOutCompanyEmployees = group.Count(e => e.WorkPlaceStatus == WorkPlaceStatus.OutCompany)
                })
                .SingleOrDefaultAsync(cancellationToken);
            
            return summary is null
                ? (0, new EmployeeSummaryResponse(
                    TotalMonthlyEmployees: 0,
                    TotalDailyEmployees: 0,
                    TotalActiveEmployees: 0,
                    TotalInactiveEmployees: 0,
                    TotalInCompanyEmployees: 0,
                    TotalOutCompanyEmployees: 0))
                : (summary.TotalCount, new EmployeeSummaryResponse(                
                    TotalMonthlyEmployees: summary.TotalMonthlyEmployees,
                    TotalDailyEmployees: summary.TotalDailyEmployees,
                    TotalActiveEmployees: summary.TotalActiveEmployees,
                    TotalInactiveEmployees: summary.TotalInactiveEmployees,
                    TotalInCompanyEmployees: summary.TotalInCompanyEmployees,
                    TotalOutCompanyEmployees: summary.TotalOutCompanyEmployees
                ));
        }
        private async Task<(Employee?, IEnumerable<MiniErp.Domain.Entities.Employees.EmployeeAttendance>)> LoadForWriteAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var employee = await dbContext.Employees
                .Where(employee => employee.CompanyId == campanyId)
                .FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);
            var attendances = await dbContext.EmployeeAttendances
                .Where(attendance => attendance.Employee.CompanyId == campanyId)
                .Where(attendance => attendance.EmployeeId == id).ToListAsync(cancellationToken);
            return (employee, attendances);
        }

        /// <summary>
        /// Applies a DB-level balance-status filter using correlated subqueries that mirror
        /// the same three sources used by <c>CreateEmployeeRows</c> in
        /// <c>FinancialStatementService.Employee.cs</c>:
        /// <list type="number">
        ///   <item>EmployeeOpeningBalances (fiscal-year scoped)</item>
        ///   <item>EmployeeMovements (fiscal-year scoped)</item>
        ///   <item>Posted JournalEntryLines where PartyType = Employee (fiscal-year scoped)</item>
        /// </list>
        /// Balance = SUM(Credit) − SUM(Debit). Positive → company owes employee (Credit Balance).
        /// </summary>
        internal IQueryable<Employee> ApplyBalanceFilter(
            IQueryable<Employee> query,
            BalanceStatus status,
            int fiscalYearId)
        {
            if (status == BalanceStatus.All)
                return query;

            // Source 1 – opening balances (non-payroll entries map Credit type → positive)
            // Source 2 – movements (Credit and Bonus → positive; Debit and Deduction → negative)
            // Source 3 – posted manual journal lines tagged to the employee
            // All three use credit − debit = net signed amount.

            var postedLines = PostedJournalLedgerLines.Create(dbContext, campanyId)
                .Where(line =>
                    line.PartyType == JournalPartyType.Employee &&
                    line.PartyId.HasValue &&
                    line.JournalEntry.FiscalYearId == fiscalYearId &&
                    line.JournalEntry.SourceType != JournalEntrySourceType.EmployeeOpeningBalance &&
                    line.JournalEntry.SourceType != JournalEntrySourceType.CashVoucher);

            return status switch
            {
                // Credit Balance: company owes employee → net > 0
                BalanceStatus.CreditBalance => query.Where(emp =>
                    (
                        (dbContext.EmployeeOpeningBalances
                            .Where(b =>
                                b.CompanyId == campanyId &&
                                b.FiscalYearId == fiscalYearId &&
                                b.EmployeeId == emp.Id)
                            .Sum(b => b.BalanceType == EmployeeBalanceType.Credit
                                ? (decimal?)b.Amount
                                : -(decimal?)b.Amount) ?? 0m)
                        +
                        (dbContext.EmployeeMovements
                            .Where(m =>
                                m.CompanyId == campanyId &&
                                m.FiscalYearId == fiscalYearId &&
                                m.EmployeeId == emp.Id)
                            .Sum(m => (decimal?)(m.Credit - m.Debit)) ?? 0m)
                        +
                        (postedLines
                            .Where(line => line.PartyId == emp.Id)
                            .Sum(line => (decimal?)(line.Credit - line.Debit)) ?? 0m)
                    ) > 0),

                // Debit Balance: employee owes company → net < 0
                BalanceStatus.DebitBalance => query.Where(emp =>
                    (
                        (dbContext.EmployeeOpeningBalances
                            .Where(b =>
                                b.CompanyId == campanyId &&
                                b.FiscalYearId == fiscalYearId &&
                                b.EmployeeId == emp.Id)
                            .Sum(b => b.BalanceType == EmployeeBalanceType.Credit
                                ? (decimal?)b.Amount
                                : -(decimal?)b.Amount) ?? 0m)
                        +
                        (dbContext.EmployeeMovements
                            .Where(m =>
                                m.CompanyId == campanyId &&
                                m.FiscalYearId == fiscalYearId &&
                                m.EmployeeId == emp.Id)
                            .Sum(m => (decimal?)(m.Credit - m.Debit)) ?? 0m)
                        +
                        (postedLines
                            .Where(line => line.PartyId == emp.Id)
                            .Sum(line => (decimal?)(line.Credit - line.Debit)) ?? 0m)
                    ) < 0),

                // Zero Balance: no transactions at all, or the net is exactly zero
                BalanceStatus.ZeroBalance => query.Where(emp =>
                    (
                        (dbContext.EmployeeOpeningBalances
                            .Where(b =>
                                b.CompanyId == campanyId &&
                                b.FiscalYearId == fiscalYearId &&
                                b.EmployeeId == emp.Id)
                            .Sum(b => b.BalanceType == EmployeeBalanceType.Credit
                                ? (decimal?)b.Amount
                                : -(decimal?)b.Amount) ?? 0m)
                        +
                        (dbContext.EmployeeMovements
                            .Where(m =>
                                m.CompanyId == campanyId &&
                                m.FiscalYearId == fiscalYearId &&
                                m.EmployeeId == emp.Id)
                            .Sum(m => (decimal?)(m.Credit - m.Debit)) ?? 0m)
                        +
                        (postedLines
                            .Where(line => line.PartyId == emp.Id)
                            .Sum(line => (decimal?)(line.Credit - line.Debit)) ?? 0m)
                    ) == 0),

                _ => query
            };
        }

        /// <summary>
        /// Batch-fetches the signed balance for a set of employee IDs in a single round-trip.
        /// Unions the same three sources used by <c>CreateEmployeeRows</c>.
        /// Returns a dictionary keyed by employee ID. Employees with no transactions return 0.
        /// </summary>
        internal async Task<Dictionary<int, decimal>> FetchEmployeeBalancesAsync(
            IReadOnlyCollection<int> employeeIds,
            int fiscalYearId,
            CancellationToken cancellationToken)
        {
            if (employeeIds.Count == 0)
                return [];

            var ids = employeeIds.ToArray();

            var postedLines = PostedJournalLedgerLines.Create(dbContext, campanyId)
                .Where(line =>
                    line.PartyType == JournalPartyType.Employee &&
                    line.PartyId.HasValue &&
                    ids.Contains(line.PartyId!.Value) &&
                    line.JournalEntry.FiscalYearId == fiscalYearId &&
                    line.JournalEntry.SourceType != JournalEntrySourceType.EmployeeOpeningBalance &&
                    line.JournalEntry.SourceType != JournalEntrySourceType.CashVoucher);

            // Opening balances grouped by employee
            var openingTotals = await dbContext.EmployeeOpeningBalances
                .Where(b =>
                    b.CompanyId == campanyId &&
                    b.FiscalYearId == fiscalYearId &&
                    ids.Contains(b.EmployeeId))
                .GroupBy(b => b.EmployeeId)
                .Select(g => new
                {
                    EmployeeId = g.Key,
                    Signed = g.Sum(b =>
                        b.BalanceType == EmployeeBalanceType.Credit
                            ? (decimal?)b.Amount
                            : -(decimal?)b.Amount)
                })
                .ToListAsync(cancellationToken);

            // Movements grouped by employee
            var movementTotals = await dbContext.EmployeeMovements
                .Where(m =>
                    m.CompanyId == campanyId &&
                    m.FiscalYearId == fiscalYearId &&
                    ids.Contains(m.EmployeeId))
                .GroupBy(m => m.EmployeeId)
                .Select(g => new
                {
                    EmployeeId = g.Key,
                    Signed = g.Sum(m => (decimal?)(m.Credit - m.Debit))
                })
                .ToListAsync(cancellationToken);

            // Posted journal lines grouped by employee
            var journalTotals = await postedLines
                .GroupBy(line => line.PartyId!.Value)
                .Select(g => new
                {
                    EmployeeId = g.Key,
                    Signed = g.Sum(line => (decimal?)(line.Credit - line.Debit))
                })
                .ToListAsync(cancellationToken);

            // Merge all three sources into a single balance per employee
            var result = new Dictionary<int, decimal>();
            foreach (var id in ids)
            {
                var opening  = openingTotals.FirstOrDefault(x => x.EmployeeId == id)?.Signed ?? 0m;
                var movement = movementTotals.FirstOrDefault(x => x.EmployeeId == id)?.Signed ?? 0m;
                var journal  = journalTotals.FirstOrDefault(x => x.EmployeeId == id)?.Signed ?? 0m;
                result[id]   = opening + movement + journal;
            }

            return result;
        }
    }
}

