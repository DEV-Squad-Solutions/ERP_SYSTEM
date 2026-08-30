using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.EmployeeOpeningBalances;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Tests.PayrollEntries;
using System;
using System.Threading.Tasks;
using Xunit;
using AttendanceEntity = MiniErp.Domain.Entities.Employees.EmployeeAttendance;

namespace MiniErp.Tests.EmployeeFinancials;

public sealed class EmployeeFinancialStatementTests
{
    [Fact]
    public async Task GetEmployeeStatementAsync_ShouldAccuratelyCalculateRunningBalance_FromOpeningBalanceSalaryAndMovements()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var openingBalanceService = database.CreateOpeningBalanceService();
        var payrollService = database.CreatePayrollService();
        var statementService = database.CreateStatementService();

        // 1. Initial Opening Balance (Credit +5,000) on 2026-08-01
        await openingBalanceService.AddAsync(new EmployeeOpeningBalanceRequest(
            EmployeeId: 1,
            DocumentDate: new DateOnly(2026, 8, 1),
            Currency: CurrencyCode.EGP,
            BalanceType: EmployeeBalanceType.Credit,
            Amount: 5000m,
            Notes: "Initial balance"));

        // 2. Attendance and Salary Transfer (Net +2,000) on 2026-08-10
        for (int day = 1; day <= 10; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 8, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var payrollResult = await payrollService.AddAsync(new PayrollEntryCreateRequest(
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 10),
            EmployeeId: 1));
        Assert.True(payrollResult.IsSuccess);

        var salaryTransferResult = await payrollService.MoveSalaryForEmployeeAccountAsync(
            payrollResult.Value.Id,
            new PayrollEntrySalaryPaymentRequest(
                PostingDate: new DateOnly(2026, 8, 10),
                Notes: "Salary Transfer Period 1"));
        Assert.True(salaryTransferResult.IsSuccess);
        // NetSalary = 10 days * 200 = 2000

        // 3. Movement: Cash Advance (Debit -500) on 2026-08-15
        var advance = new EmployeeMovement
        {
            CompanyId = 1,
            EmployeeId = 1,
            Type = EmployeeMovementType.Advance,
            MovementDate = new DateOnly(2026, 8, 15),
            Currency = CurrencyCode.EGP,
            Notes = "Mid-month advance"
        };
        advance.ApplyAmounts(EmployeeMovementType.Advance, 500m);
        advance.ApplyExchangeRate(1m);
        database.Context.EmployeeMovements.Add(advance);

        // 4. Movement: Performance Bonus (Credit +1,000) on 2026-08-20
        var bonus = new EmployeeMovement
        {
            CompanyId = 1,
            EmployeeId = 1,
            Type = EmployeeMovementType.Bonus,
            MovementDate = new DateOnly(2026, 8, 20),
            Currency = CurrencyCode.EGP,
            Notes = "Special bonus"
        };
        bonus.ApplyAmounts(EmployeeMovementType.Bonus, 1000m);
        bonus.ApplyExchangeRate(1m);
        database.Context.EmployeeMovements.Add(bonus);
        await database.Context.SaveChangesAsync();

        // Act - Get complete statement
        var statementResult = await statementService.GetEmployeeStatementAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            new EmployeeStatementFilterRequest(EmployeeId: 1));

        // Assert
        Assert.True(statementResult.IsSuccess);
        var response = statementResult.Value;

        Assert.Equal(1, response.EmployeeId);
        Assert.Equal(4, response.Items.Count);

        // Item 1: Opening Balance (+5000) -> Running Balance = 5,000
        Assert.Equal(EmployeeStatementSourceType.OpeningBalance, response.Items[0].SourceType);
        Assert.Equal(0m, response.Items[0].DebitAmount);
        Assert.Equal(5000m, response.Items[0].CreditAmount);
        Assert.Equal(5000m, response.Items[0].BalanceAmount);

        // Item 2: Salary Transfer (+2000) -> Running Balance = 7,000
        Assert.Equal(EmployeeStatementSourceType.SalaryTransfer, response.Items[1].SourceType);
        Assert.Equal(0m, response.Items[1].DebitAmount);
        Assert.Equal(2000m, response.Items[1].CreditAmount);
        Assert.Equal(7000m, response.Items[1].BalanceAmount);

        // Item 3: Advance (-500) -> Running Balance = 6,500
        Assert.Equal(EmployeeStatementSourceType.Movement, response.Items[2].SourceType);
        Assert.Equal(500m, response.Items[2].DebitAmount);
        Assert.Equal(0m, response.Items[2].CreditAmount);
        Assert.Equal(6500m, response.Items[2].BalanceAmount);

        // Item 4: Bonus (+1000) -> Running Balance = 7,500
        Assert.Equal(EmployeeStatementSourceType.Movement, response.Items[3].SourceType);
        Assert.Equal(0m, response.Items[3].DebitAmount);
        Assert.Equal(1000m, response.Items[3].CreditAmount);
        Assert.Equal(7500m, response.Items[3].BalanceAmount);

        // Summary Assertions
        Assert.Equal(8000m, response.Summary.TotalCredits); // 5000 + 2000 + 1000
        Assert.Equal(500m, response.Summary.TotalDebits);    // 500
        Assert.Equal(7500m, response.Summary.ClosingBalanceAmount);

        // Act & Assert Balance Endpoint
        var balanceResult = await statementService.GetEmployeeBalanceAsync(1);
        Assert.True(balanceResult.IsSuccess);
        Assert.Equal(7500m, balanceResult.Value.BalanceAmount);
        Assert.Equal(8000m, balanceResult.Value.TotalCredits);
        Assert.Equal(500m, balanceResult.Value.TotalDebits);

        // Act & Assert Account Summary
        var summaryResult = await statementService.GetEmployeeAccountSummaryAsync(1);
        Assert.True(summaryResult.IsSuccess);
        Assert.Equal(1, summaryResult.Value.Employee.Id);
        Assert.Equal(5000m, summaryResult.Value.OpeningBalance);
        Assert.Equal(7500m, summaryResult.Value.CurrentBalance);
        Assert.Equal(8000m, summaryResult.Value.TotalCredits);
        Assert.Equal(500m, summaryResult.Value.TotalDebits);
        Assert.Equal(500m, summaryResult.Value.TotalAdvances);
        Assert.Equal(1000m, summaryResult.Value.TotalBonuses);
        Assert.Equal(2000m, summaryResult.Value.TotalSalaryMoved);
    }
}
