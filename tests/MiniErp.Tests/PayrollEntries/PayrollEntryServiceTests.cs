using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using AttendanceEntity = MiniErp.Domain.Entities.Employees.EmployeeAttendance;

namespace MiniErp.Tests.PayrollEntries;

public sealed class PayrollEntryServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldCreatePayrollEntry_ForSingleEmployee()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var startDate = new DateOnly(2026, 8, 1);
        var endDate = new DateOnly(2026, 8, 10);

        // Seed 10 days attendance for employee 1 (Monthly: 6000 salary / 30 required days = 200/day)
        for (int day = 1; day <= 10; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 8, day),
                Status = AttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var request = new PayrollEntryCreateRequest(
            StartDate: startDate,
            EndDate: endDate,
            CashboxVoucherId: null,
            CashboxId: null,
            EmployeeId: 1,
            Bonus: 100m,
            Deduction: 50m);

        // Act
        var result = await service.AddAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.EmployeeId);
        Assert.Equal("Monthly Employee", result.Value.EmployeeName);
        Assert.Equal(6000m, result.Value.GrossSalary);
        // 10 days * 200 = 2000 + 100 bonus - 50 deduction = 2050
        Assert.Equal(2050m, result.Value.NetSalary);
        Assert.False(result.Value.IsSalaryMoveToEmployeeAccount);
        Assert.Equal(10, result.Value.AttendanceSummary.PresentDays);
    }

    [Fact]
    public async Task MoveSalaryForEmployeeAccountAsync_ShouldCreditEmployeeTransaction_AndMarkAsMoved()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var startDate = new DateOnly(2026, 8, 1);
        var endDate = new DateOnly(2026, 8, 5);

        for (int day = 1; day <= 5; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 8, day),
                Status = AttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var addResult = await service.AddAsync(new PayrollEntryCreateRequest(
            StartDate: startDate,
            EndDate: endDate,
            CashboxVoucherId: null,
            CashboxId: null,
            EmployeeId: 1));

        Assert.True(addResult.IsSuccess);
        var entryId = addResult.Value.Id;

        // Act - Move salary to employee account
        var payDate = new DateOnly(2026, 8, 6);
        var moveResult = await service.MoveSalaryForEmployeeAccountAsync(
            entryId,
            new PayrollEntrySalaryPaymentRequest(
                PostingDate: payDate,
                Notes: "Salary transfer for August period 1"));

        // Assert
        Assert.True(moveResult.IsSuccess);
        Assert.True(moveResult.Value.IsSalaryMoveToEmployeeAccount);

        // Verify EmployeeTransaction account ledger record
        var transactions = await database.Context.EmployeeTransactions
            .Where(t => t.CompanyId == 1 && t.EmployeeId == 1)
            .ToListAsync();

        Assert.Single(transactions);
        var tx = transactions[0];
        Assert.Equal(EmployeeTransactionType.Credit, tx.Type);
        Assert.Equal(EmployeeTransactionSource.Payroll, tx.SourceType);
        Assert.Equal(entryId, tx.SourceId);
        Assert.Equal(moveResult.Value.NetSalary, tx.Amount);
        Assert.Equal(moveResult.Value.NetSalary, tx.RunningBalance);
        Assert.Equal(payDate, tx.TransactionDate);

        // Verify Employee LastDayOfReceivingSalary updated
        var employee = await database.Context.Employees.FindAsync(1);
        Assert.NotNull(employee);
        Assert.Equal(endDate, employee.LastDayOfReceivingSalary);
    }

    [Fact]
    public async Task AddBulkAsync_ShouldCreatePayrollEntries_ForMultipleEmployees()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var startDate = new DateOnly(2026, 8, 1);
        var endDate = new DateOnly(2026, 8, 5);

        // Seed 5 days attendance for Monthly employee (Emp 1)
        for (int day = 1; day <= 5; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 8, day),
                Status = AttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay,
                WorkOverTimeRatio = day == 1 ? WorkDayRatio.HalfDay : null // 0.5 overtime day
            });
        }

        // Seed 3 days attendance for Daily employee (Emp 2: 200/day)
        for (int day = 1; day <= 3; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 2,
                WorkDate = new DateOnly(2026, 8, day),
                Status = AttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var bulkRequest = new BulkPayrollEntryCreateRequest(
            Entries:
            [
                new IndividualPayrollEntryCreateRequest(
                    EmployeeId: 1,
                    StartDate: startDate,
                    EndDate: endDate,
                    Bonus: 200m,
                    Deduction: 50m),
                new IndividualPayrollEntryCreateRequest(
                    EmployeeId: 2,
                    StartDate: startDate,
                    EndDate: endDate,
                    Bonus: null,
                    Deduction: null)
            ]);

        // Act
        var result = await service.AddBulkAsync(bulkRequest);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var emp1Entry = result.Value.FirstOrDefault(e => e.EmployeeId == 1);
        Assert.NotNull(emp1Entry);
        // Monthly: 6000/30 = 200/day. WorkedUnits: 5 + 0.5 = 5.5. Calculated: 200 * 5.5 = 1100 + 200 bonus - 50 deduction = 1250
        Assert.Equal(1250m, emp1Entry.NetSalary);
        Assert.Equal(5, emp1Entry.AttendanceSummary.PresentDays);
        Assert.Equal(0.5m, emp1Entry.AttendanceSummary.TotalOvertimeDays);
        Assert.False(emp1Entry.IsSalaryMoveToEmployeeAccount);

        var emp2Entry = result.Value.FirstOrDefault(e => e.EmployeeId == 2);
        Assert.NotNull(emp2Entry);
        // Daily: 200/day * 3 days = 600
        Assert.Equal(600m, emp2Entry.NetSalary);
        Assert.Equal(3, emp2Entry.AttendanceSummary.PresentDays);
        Assert.False(emp2Entry.IsSalaryMoveToEmployeeAccount);

        // Verify DB persistence
        var dbEntries = await database.Context.PayrollEntries.Where(p => p.CompanyId == 1).ToListAsync();
        Assert.Equal(2, dbEntries.Count);
    }

    [Fact]
    public async Task MoveSalaryForEmployeeAccountBulkAsync_ShouldCreditAllEmployeeAccounts_AndMarkAllAsMoved()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var startDate = new DateOnly(2026, 8, 1);
        var endDate = new DateOnly(2026, 8, 5);

        for (int day = 1; day <= 5; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 8, day),
                Status = AttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 2,
                WorkDate = new DateOnly(2026, 8, day),
                Status = AttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var bulkAddResult = await service.AddBulkAsync(new BulkPayrollEntryCreateRequest(
            Entries:
            [
                new IndividualPayrollEntryCreateRequest(EmployeeId: 1, StartDate: startDate, EndDate: endDate),
                new IndividualPayrollEntryCreateRequest(EmployeeId: 2, StartDate: startDate, EndDate: endDate)
            ]));

        Assert.True(bulkAddResult.IsSuccess);
        Assert.Equal(2, bulkAddResult.Value.Count);

        var entryIds = bulkAddResult.Value.Select(e => e.Id).ToList();

        // Act - Bulk move salary
        var payDate = new DateOnly(2026, 8, 6);
        var moveResult = await service.MoveSalaryForEmployeeAccountBulkAsync(
            new BulkPayrollEntrySalaryPaymentRequest(
                PayrollEntryIds: entryIds,
                DefaultPostingDate: payDate,
                Notes: "Bulk salary credit for period"));

        // Assert
        Assert.True(moveResult.IsSuccess);
        Assert.Equal(2, moveResult.Value.Count);
        Assert.All(moveResult.Value, e => Assert.True(e.IsSalaryMoveToEmployeeAccount));

        // Verify transactions created for both employees
        var tx1 = await database.Context.EmployeeTransactions
            .FirstOrDefaultAsync(t => t.CompanyId == 1 && t.EmployeeId == 1);
        Assert.NotNull(tx1);
        Assert.Equal(EmployeeTransactionType.Credit, tx1.Type);
        Assert.Equal(EmployeeTransactionSource.Payroll, tx1.SourceType);
        Assert.Equal(1000m, tx1.Amount); // 5 * 200
        Assert.Equal(1000m, tx1.RunningBalance);

        var tx2 = await database.Context.EmployeeTransactions
            .FirstOrDefaultAsync(t => t.CompanyId == 1 && t.EmployeeId == 2);
        Assert.NotNull(tx2);
        Assert.Equal(EmployeeTransactionType.Credit, tx2.Type);
        Assert.Equal(EmployeeTransactionSource.Payroll, tx2.SourceType);
        Assert.Equal(1000m, tx2.Amount); // 5 * 200
        Assert.Equal(1000m, tx2.RunningBalance);

        // Verify employees' LastDayOfReceivingSalary
        var emp1 = await database.Context.Employees.FindAsync(1);
        var emp2 = await database.Context.Employees.FindAsync(2);
        Assert.Equal(endDate, emp1!.LastDayOfReceivingSalary);
        Assert.Equal(endDate, emp2!.LastDayOfReceivingSalary);
    }

    [Fact]
    public async Task AddBulkAsync_ShouldFail_WhenDuplicateEmployeesInRequest()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var bulkRequest = new BulkPayrollEntryCreateRequest(
            Entries:
            [
                new IndividualPayrollEntryCreateRequest(EmployeeId: 1),
                new IndividualPayrollEntryCreateRequest(EmployeeId: 1)
            ]);

        // Act
        var result = await service.AddBulkAsync(bulkRequest);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("PayrollEntry.DuplicateEmployee", result.Error.Code);
    }

    [Fact]
    public async Task MoveSalaryForEmployeeAccountBulkAsync_ShouldFail_WhenEntryAlreadyPaid()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        database.Context.EmployeeAttendances.Add(new AttendanceEntity
        {
            CompanyId = 1,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 8, 1),
            Status = AttendanceStatus.Present,
            WorkDayRatio = WorkDayRatio.FullDay
        });
        await database.Context.SaveChangesAsync();

        var addResult = await service.AddAsync(new PayrollEntryCreateRequest(
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 1),
            CashboxVoucherId: null,
            CashboxId: null,
            EmployeeId: 1));

        Assert.True(addResult.IsSuccess);
        var entryId = addResult.Value.Id;

        // Pay once
        var payResult = await service.MoveSalaryForEmployeeAccountAsync(
            entryId,
            new PayrollEntrySalaryPaymentRequest(PostingDate: new DateOnly(2026, 8, 2)));
        Assert.True(payResult.IsSuccess);

        // Act - Try bulk paying again
        var bulkMoveResult = await service.MoveSalaryForEmployeeAccountBulkAsync(
            new BulkPayrollEntrySalaryPaymentRequest(PayrollEntryIds: [entryId]));

        // Assert
        Assert.True(bulkMoveResult.IsFailure);
        Assert.Equal("PayrollEntry.AlreadyPaid", bulkMoveResult.Error.Code);
    }
}
