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

        var emp = await database.Context.Employees.FindAsync(1);
        emp!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

        var endDate = new DateOnly(2026, 8, 10);

        // Seed 10 days attendance for employee 1 (Monthly: 6000 salary / 30 required days = 200/day)
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

        var request = new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: endDate,
            Bonus: 100m,
            Deduction: 50m);

        // Act
        var result = await service.AddAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.EmployeeId);
        Assert.Equal(new DateOnly(2026, 8, 1), result.Value.StartDate);
        Assert.Equal(endDate, result.Value.EndDate);
        Assert.Equal("Monthly Employee", result.Value.EmployeeName);
        Assert.Equal(6000m, result.Value.GrossSalary);
        // 10 days * 200 = 2000 + 100 bonus - 50 deduction = 2050
        Assert.Equal(2050m, result.Value.NetSalary);
        Assert.False(result.Value.IsSalaryMoveToEmployeeAccount);
        Assert.Equal(10, result.Value.AttendanceSummary.PresentDays);
    }

    [Fact]
    public async Task MoveSalaryForEmployeeAccountAsync_ShouldCreateEmployeeOpeningBalance_AndMarkAsMoved()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp = await database.Context.Employees.FindAsync(1);
        emp!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

        var endDate = new DateOnly(2026, 8, 5);

        for (int day = 1; day <= 5; day++)
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

        var addResult = await service.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: endDate));

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
        Assert.Equal(payDate, moveResult.Value.SalaryMovedOn);

        // Verify EmployeeOpeningBalance account ledger record
        var openingBalances = await database.Context.EmployeeOpeningBalances
            .Where(b => b.CompanyId == 1 && b.EmployeeId == 1)
            .ToListAsync();

        Assert.Single(openingBalances);
        var ob = openingBalances[0];
        Assert.Equal(EmployeeBalanceType.Credit, ob.BalanceType);
        Assert.Equal(entryId, ob.PayrollEntryId);
        Assert.Equal(moveResult.Value.NetSalary, ob.Amount);
        Assert.Equal(payDate, ob.DocumentDate);
        Assert.StartsWith("EOB-", ob.DocumentNumber);

        // Verify Employee LastDayOfReceivingSalary updated
        var employee = await database.Context.Employees.FindAsync(1);
        Assert.NotNull(employee);
        Assert.Equal(endDate, employee.LastDayOfReceivingSalary);
    }

    [Fact]
    public async Task MoveSalaryForEmployeeAccountAsync_ShouldBeIdempotent_AndRejectDuplicateTransfer()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp = await database.Context.Employees.FindAsync(1);
        emp!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

        database.Context.EmployeeAttendances.Add(new AttendanceEntity
        {
            CompanyId = 1,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 8, 1),
            Status = EmployeeAttendanceStatus.Present,
            WorkDayRatio = WorkDayRatio.FullDay
        });
        await database.Context.SaveChangesAsync();

        var addResult = await service.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 8, 1)));

        Assert.True(addResult.IsSuccess);
        var entryId = addResult.Value.Id;

        // First transfer - Should succeed
        var firstTransfer = await service.MoveSalaryForEmployeeAccountAsync(
            entryId,
            new PayrollEntrySalaryPaymentRequest(PostingDate: new DateOnly(2026, 8, 2)));
        Assert.True(firstTransfer.IsSuccess);

        // Act - Second transfer attempt on the same payroll entry
        var secondTransfer = await service.MoveSalaryForEmployeeAccountAsync(
            entryId,
            new PayrollEntrySalaryPaymentRequest(PostingDate: new DateOnly(2026, 8, 2)));

        // Assert - Rejected with Conflict
        Assert.True(secondTransfer.IsFailure);
        Assert.Equal("PayrollEntry.AlreadyPaid", secondTransfer.Error.Code);

        // Ensure only 1 opening balance was created
        var openingBalancesCount = await database.Context.EmployeeOpeningBalances
            .CountAsync(b => b.CompanyId == 1 && b.PayrollEntryId == entryId);
        Assert.Equal(1, openingBalancesCount);
    }

    [Fact]
    public async Task AddBulkAsync_ShouldCreatePayrollEntries_ForMultipleEmployees()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp1 = await database.Context.Employees.FindAsync(1);
        emp1!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        var emp2 = await database.Context.Employees.FindAsync(2);
        emp2!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

        var endDate = new DateOnly(2026, 8, 5);

        // Seed 5 days attendance for Monthly employee (Emp 1)
        for (int day = 1; day <= 5; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 8, day),
                Status = EmployeeAttendanceStatus.Present,
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
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var bulkRequest = new BulkPayrollEntryCreateRequest(
            Entries:
            [
                new IndividualPayrollEntryCreateRequest(
                    EmployeeId: 1,
                    EndDate: endDate,
                    Bonus: 200m,
                    Deduction: 50m),
                new IndividualPayrollEntryCreateRequest(
                    EmployeeId: 2,
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

        var emp1 = await database.Context.Employees.FindAsync(1);
        emp1!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        var emp2 = await database.Context.Employees.FindAsync(2);
        emp2!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

        var endDate = new DateOnly(2026, 8, 5);

        for (int day = 1; day <= 5; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 8, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 2,
                WorkDate = new DateOnly(2026, 8, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var bulkAddResult = await service.AddBulkAsync(new BulkPayrollEntryCreateRequest(
            Entries:
            [
                new IndividualPayrollEntryCreateRequest(EmployeeId: 1, EndDate: endDate),
                new IndividualPayrollEntryCreateRequest(EmployeeId: 2, EndDate: endDate)
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

        // Verify opening balances created for both employees
        var ob1 = await database.Context.EmployeeOpeningBalances
            .FirstOrDefaultAsync(b => b.CompanyId == 1 && b.EmployeeId == 1);
        Assert.NotNull(ob1);
        Assert.Equal(EmployeeBalanceType.Credit, ob1.BalanceType);
        Assert.Equal(1000m, ob1.Amount); // 5 * 200

        var ob2 = await database.Context.EmployeeOpeningBalances
            .FirstOrDefaultAsync(b => b.CompanyId == 1 && b.EmployeeId == 2);
        Assert.NotNull(ob2);
        Assert.Equal(EmployeeBalanceType.Credit, ob2.BalanceType);
        Assert.Equal(1000m, ob2.Amount); // 5 * 200

        // Verify employees' LastDayOfReceivingSalary
        var emp1Reloaded = await database.Context.Employees.FindAsync(1);
        var emp2Reloaded = await database.Context.Employees.FindAsync(2);
        Assert.Equal(endDate, emp1Reloaded!.LastDayOfReceivingSalary);
        Assert.Equal(endDate, emp2Reloaded!.LastDayOfReceivingSalary);
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

        var emp = await database.Context.Employees.FindAsync(1);
        emp!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

        database.Context.EmployeeAttendances.Add(new AttendanceEntity
        {
            CompanyId = 1,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 8, 1),
            Status = EmployeeAttendanceStatus.Present,
            WorkDayRatio = WorkDayRatio.FullDay
        });
        await database.Context.SaveChangesAsync();

        var addResult = await service.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 8, 1)));

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

    [Fact]
    public async Task UpdateAsync_And_DeleteAsync_ShouldFail_WhenSalaryAlreadyMoved()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp = await database.Context.Employees.FindAsync(1);
        emp!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

        database.Context.EmployeeAttendances.Add(new AttendanceEntity
        {
            CompanyId = 1,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 8, 1),
            Status = EmployeeAttendanceStatus.Present,
            WorkDayRatio = WorkDayRatio.FullDay
        });
        await database.Context.SaveChangesAsync();

        var addResult = await service.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 8, 1)));

        Assert.True(addResult.IsSuccess);
        var entryId = addResult.Value.Id;

        // Move salary
        var moveResult = await service.MoveSalaryForEmployeeAccountAsync(
            entryId,
            new PayrollEntrySalaryPaymentRequest(PostingDate: new DateOnly(2026, 8, 2)));
        Assert.True(moveResult.IsSuccess);

        // Act & Assert Update
        var updateResult = await service.UpdateAsync(
            entryId,
            new PayrollEntryUpdateRequest(EmployeeId: 1, Bonus: 500m));
        Assert.True(updateResult.IsFailure);
        Assert.Equal("PayrollEntry.AlreadyPaid", updateResult.Error.Code);

        // Act & Assert Delete
        var deleteResult = await service.DeleteAsync(entryId);
        Assert.True(deleteResult.IsFailure);
        Assert.Equal("PayrollEntry.AlreadyPaid", deleteResult.Error.Code);
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldCalculateMetricsCorrectly()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp = await database.Context.Employees.FindAsync(1);
        emp!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

        // 1. Seed attendance
        for (int day = 1; day <= 5; day++)
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

        // 2. Add payroll entry
        var addResult = await service.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 8, 5),
            Bonus: 100m,
            Deduction: 50m));
        Assert.True(addResult.IsSuccess);

        // 3. Move salary
        var moveResult = await service.MoveSalaryForEmployeeAccountAsync(
            addResult.Value.Id,
            new PayrollEntrySalaryPaymentRequest(PostingDate: new DateOnly(2026, 8, 5)));
        Assert.True(moveResult.IsSuccess);

        // 4. Add advance movement
        var movementService = database.CreateMovementService();
        var cashbox = new MiniErp.Domain.Entities.CashManagement.Cashbox
        {
            CompanyId = 1,
            Code = "CB-01",
            Name = "Main Box",
            Currency = CurrencyCode.EGP,
            OpeningBalance = 1_000m,
            IsActive = true
        };
        database.Context.Cashboxes.Add(cashbox);
        await database.Context.SaveChangesAsync();

        var advanceResult = await movementService.AddAsync(new MiniErp.Application.Features.EmployeeMovements.EmployeeMovementRequest(
            EmployeeId: 1,
            Type: EmployeeMovementType.Advance,
            Amount: 300m,
            Currency: CurrencyCode.EGP,
            MovementDate: new DateOnly(2026, 8, 6),
            CashboxId: cashbox.Id));
        Assert.True(advanceResult.IsSuccess);

        // Act - Get Dashboard
        var dashboardResult = await service.GetDashboardAsync(new PayrollDashboardFilterRequest());

        // Assert
        Assert.True(dashboardResult.IsSuccess);
        var dashboard = dashboardResult.Value;

        Assert.Equal(6000m, dashboard.TotalPayrolls); // Gross salary
        Assert.Equal(1050m, dashboard.NetPayable);    // 5 days * 200 + 100 - 50 = 1050
        Assert.Equal(1050m, dashboard.TotalPaid);     // Salary moved
        Assert.Equal(50m, dashboard.TotalDeductions); // 50
        Assert.Equal(300m, dashboard.TotalAdvances);  // 300
        Assert.True(dashboard.EmployeeCount >= 1);
        Assert.NotEmpty(dashboard.RecentOperations);
    }

    [Fact]
    public async Task AddOutCompanyAsync_ShouldCreatePayrollEntry_WithoutAttendanceRecords()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 50,
            CompanyId = 1,
            Code = "OUT001",
            Name = "External Worker",
            Type = EmployeeType.Daily,
            DailySalary = 300m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Desert Site");
        outEmp.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        var request = new OutCompanyPayrollEntryRequest(
            EmployeeId: 50,
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 10),
            PresentDays: 8,
            WorkedDaysByDayUnit: 8m,
            OvertimeByDayUnit: 2m,
            DeductionByDayUnit: 1m,
            Bonus: 150m,
            Deduction: 50m);

        // Act
        var result = await service.AddOutCompanyAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value.EmployeeId);
        Assert.Equal(new DateOnly(2026, 8, 1), result.Value.StartDate);
        Assert.Equal(300m, result.Value.GrossSalary);
        // worked units = 8 + 2 - 1 = 9 units * 300 = 2700 calculated
        Assert.Equal(2700m, result.Value.CalculatedSalary);
        // net = 2700 + 150 - 50 = 2800
        Assert.Equal(2800m, result.Value.NetSalary);
        Assert.False(result.Value.IsSalaryMoveToEmployeeAccount);
        Assert.Equal(8, result.Value.AttendanceSummary.PresentDays);
        Assert.Equal(8m, result.Value.AttendanceSummary.TotalPresentDays);
        Assert.Equal(2m, result.Value.AttendanceSummary.TotalOvertimeDays);
        Assert.Equal(1m, result.Value.AttendanceSummary.TotalDeductionDays);

        // Verify that no attendance rows were touched/required
        var attendances = await database.Context.EmployeeAttendances
            .Where(a => a.EmployeeId == 50)
            .ToListAsync();
        Assert.Empty(attendances);
    }

    [Fact]
    public async Task AddOutCompanyAsync_ShouldFail_WhenEmployeeIsInCompany()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        // Employee 1 is InCompany
        var request = new OutCompanyPayrollEntryRequest(
            EmployeeId: 1,
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 10),
            PresentDays: 5,
            WorkedDaysByDayUnit: 5m);

        // Act
        var result = await service.AddOutCompanyAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("PayrollEntry.NotOutCompany", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_ShouldFail_WhenEmployeeIsOutCompany()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 51,
            CompanyId = 1,
            Code = "OUT002",
            Name = "External Tech",
            Type = EmployeeType.Monthly,
            MonthlySalary = 7000m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Remote Port");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        var request = new PayrollEntryCreateRequest(
            EmployeeId: 51,
            EndDate: new DateOnly(2026, 8, 10));

        // Act
        var result = await service.AddAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("PayrollEntry.OutCompanyNotAllowedHere", result.Error.Code);
    }

    [Fact]
    public async Task MoveSalaryForEmployeeAccountAsync_ShouldAtomicallyUpdateLastDayOfReceivingSalary()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 52,
            CompanyId = 1,
            Code = "OUT003",
            Name = "Field Specialist",
            Type = EmployeeType.Monthly,
            MonthlySalary = 6000m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Station A");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        Assert.Null(outEmp.LastDayOfReceivingSalary);

        var endDate = new DateOnly(2026, 8, 15);
        var createResult = await service.AddOutCompanyAsync(new OutCompanyPayrollEntryRequest(
            EmployeeId: 52,
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: endDate,
            PresentDays: 1,
            WorkedDaysByDayUnit: 1m));
        Assert.True(createResult.IsSuccess);

        // Verify LastDayOfReceivingSalary is STILL null after creation (only moves when salary is moved)
        var empBeforeMove = await database.Context.Employees.FindAsync(52);
        Assert.Null(empBeforeMove!.LastDayOfReceivingSalary);

        // Act - Move salary
        var moveResult = await service.MoveSalaryForEmployeeAccountAsync(
            createResult.Value.Id,
            new PayrollEntrySalaryPaymentRequest(PostingDate: new DateOnly(2026, 8, 16)));
        Assert.True(moveResult.IsSuccess);

        // Assert - Atomic update to LastDayOfReceivingSalary
        var empAfterMove = await database.Context.Employees.FindAsync(52);
        Assert.Equal(endDate, empAfterMove!.LastDayOfReceivingSalary);
    }

    [Fact]
    public async Task AddOutCompanyBulkAsync_ShouldCreatePayrollEntries_ForMultipleOutCompanyEmployees()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var empA = new Employee
        {
            Id = 60,
            CompanyId = 1,
            Code = "OUT010",
            Name = "Field Tech A",
            Type = EmployeeType.Monthly,
            MonthlySalary = 6000m,
            RequiredWorkingDaysPerMonth = 30,
            IsActive = true
        };
        empA.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Site Alpha");
        empA.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));

        var empB = new Employee
        {
            Id = 61,
            CompanyId = 1,
            Code = "OUT011",
            Name = "Field Tech B",
            Type = EmployeeType.Daily,
            DailySalary = 250m,
            IsActive = true
        };
        empB.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Site Beta");
        empB.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));

        database.Context.Employees.AddRange(empA, empB);
        await database.Context.SaveChangesAsync();

        var request = new BulkOutCompanyPayrollEntryRequest(
            Entries:
            [
                new IndividualOutCompanyPayrollEntryRequest(
                    EmployeeId: 60,
                    PresentDays: 15,
                    WorkedDaysByDayUnit: 15m,
                    Bonus: 100m,
                    Deduction: 50m),
                new IndividualOutCompanyPayrollEntryRequest(
                    EmployeeId: 61,
                    PresentDays: 10,
                    WorkedDaysByDayUnit: 10m,
                    OvertimeByDayUnit: 2m)
            ],
            DefaultStartDate: new DateOnly(2026, 8, 1),
            DefaultEndDate: new DateOnly(2026, 8, 30));

        // Act
        var result = await service.AddOutCompanyBulkAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var resA = result.Value.First(r => r.EmployeeId == 60);
        // (6000 / 30) * 15 = 3000 calculated + 100 - 50 = 3050 net
        Assert.Equal(3000m, resA.CalculatedSalary);
        Assert.Equal(3050m, resA.NetSalary);
        Assert.Equal(new DateOnly(2026, 8, 1), resA.StartDate);

        var resB = result.Value.First(r => r.EmployeeId == 61);
        // (10 + 2) * 250 = 3000 calculated & net
        Assert.Equal(3000m, resB.CalculatedSalary);
        Assert.Equal(3000m, resB.NetSalary);
        Assert.Equal(new DateOnly(2026, 8, 1), resB.StartDate);
    }

    [Fact]
    public async Task AddAsync_ShouldDeriveStartDateFromLastDayOfReceivingSalary_PlusOneDay()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp = await database.Context.Employees.FindAsync(1);
        emp!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 8, 31));
        await database.Context.SaveChangesAsync();

        database.Context.EmployeeAttendances.Add(new AttendanceEntity
        {
            CompanyId = 1,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 9, 1),
            Status = EmployeeAttendanceStatus.Present,
            WorkDayRatio = WorkDayRatio.FullDay
        });
        await database.Context.SaveChangesAsync();

        // Act
        var result = await service.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 9, 15)));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Value.StartDate);
    }

    [Fact]
    public async Task AddAsync_ShouldFail_WhenPeriodOverlapsExistingPayrollEntry()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp = await database.Context.Employees.FindAsync(1);
        emp!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

        database.Context.EmployeeAttendances.Add(new AttendanceEntity
        {
            CompanyId = 1,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 8, 1),
            Status = EmployeeAttendanceStatus.Present,
            WorkDayRatio = WorkDayRatio.FullDay
        });
        await database.Context.SaveChangesAsync();

        var first = await service.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 8, 15)));
        Assert.True(first.IsSuccess);

        // Act - Try creating another payroll entry that covers the same period
        var second = await service.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 8, 20)));

        // Assert
        Assert.True(second.IsFailure);
        Assert.Equal("PayrollEntry.PeriodOverlap", second.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRecalculatePayroll_WhenEditableFieldsChange()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp = await database.Context.Employees.FindAsync(1);
        emp!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

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

        var createResult = await service.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 8, 5)));
        Assert.True(createResult.IsSuccess);
        var entryId = createResult.Value.Id;

        // Act - Update EndDate to day 10 and add bonus
        var updateResult = await service.UpdateAsync(entryId, new PayrollEntryUpdateRequest(
            EndDate: new DateOnly(2026, 8, 10),
            Bonus: 200m,
            Deduction: 50m));

        // Assert
        Assert.True(updateResult.IsSuccess);
        Assert.Equal(10, updateResult.Value.AttendanceSummary.PresentDays);
        // 10 days * 200 = 2000 calculated + 200 bonus - 50 deduction = 2150
        Assert.Equal(2000m, updateResult.Value.CalculatedSalary);
        Assert.Equal(2150m, updateResult.Value.NetSalary);
    }

    [Fact]
    public async Task UpdateBulkAsync_ShouldRecalculate_AndFailAtomicallyIfAnyEntryPaid()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp1 = await database.Context.Employees.FindAsync(1);
        emp1!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        var emp2 = await database.Context.Employees.FindAsync(2);
        emp2!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        await database.Context.SaveChangesAsync();

        for (int day = 1; day <= 5; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 8, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 2,
                WorkDate = new DateOnly(2026, 8, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var bulkAdd = await service.AddBulkAsync(new BulkPayrollEntryCreateRequest(
            Entries:
            [
                new IndividualPayrollEntryCreateRequest(EmployeeId: 1, EndDate: new DateOnly(2026, 8, 5)),
                new IndividualPayrollEntryCreateRequest(EmployeeId: 2, EndDate: new DateOnly(2026, 8, 5))
            ]));
        Assert.True(bulkAdd.IsSuccess);

        var id1 = bulkAdd.Value[0].Id;
        var id2 = bulkAdd.Value[1].Id;

        // Move salary for entry 1 ONLY
        var moveResult = await service.MoveSalaryForEmployeeAccountAsync(id1, new PayrollEntrySalaryPaymentRequest(
            PostingDate: new DateOnly(2026, 8, 6)));
        Assert.True(moveResult.IsSuccess);

        // Act - Attempt bulk update covering both entries
        var bulkUpdateResult = await service.UpdateBulkAsync(new BulkPayrollEntryUpdateRequest(
            Entries:
            [
                new IndividualPayrollEntryUpdateRequest(Id: id1, Bonus: 300m),
                new IndividualPayrollEntryUpdateRequest(Id: id2, Bonus: 300m)
            ]));

        // Assert - Atomic rejection because entry 1 is paid
        Assert.True(bulkUpdateResult.IsFailure);
        Assert.Equal("PayrollEntry.AlreadyPaid", bulkUpdateResult.Error.Code);

        // Verify entry 2 was NOT modified
        var entry2 = await database.Context.PayrollEntries.FindAsync(id2);
        Assert.Null(entry2!.Bonus);
    }

    [Fact]
    public async Task UpdateOutCompanyAsync_ShouldRecalculate_AndFailWhenLocked()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 70,
            CompanyId = 1,
            Code = "OUT020",
            Name = "Field Specialist 2",
            Type = EmployeeType.Daily,
            DailySalary = 300m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Plant A");
        outEmp.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        var createResult = await service.AddOutCompanyAsync(new OutCompanyPayrollEntryRequest(
            EmployeeId: 70,
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 10),
            PresentDays: 5,
            WorkedDaysByDayUnit: 5m));
        Assert.True(createResult.IsSuccess);
        var entryId = createResult.Value.Id;

        // Act - Update manual inputs
        var updateResult = await service.UpdateOutCompanyAsync(entryId, new OutCompanyPayrollEntryUpdateRequest(
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 10),
            PresentDays: 8,
            WorkedDaysByDayUnit: 8m,
            OvertimeByDayUnit: 2m,
            Bonus: 100m));

        // Assert
        Assert.True(updateResult.IsSuccess);
        // (8 + 2) * 300 = 3000 calculated + 100 bonus = 3100 net
        Assert.Equal(3000m, updateResult.Value.CalculatedSalary);
        Assert.Equal(3100m, updateResult.Value.NetSalary);

        // Move salary
        var moveResult = await service.MoveSalaryForEmployeeAccountAsync(entryId, new PayrollEntrySalaryPaymentRequest(
            PostingDate: new DateOnly(2026, 8, 11)));
        Assert.True(moveResult.IsSuccess);

        // Act again - Should fail with Conflict
        var secondUpdate = await service.UpdateOutCompanyAsync(entryId, new OutCompanyPayrollEntryUpdateRequest(
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 10),
            PresentDays: 8,
            WorkedDaysByDayUnit: 8m,
            Bonus: 500m));
        Assert.True(secondUpdate.IsFailure);
        Assert.Equal("PayrollEntry.AlreadyPaid", secondUpdate.Error.Code);
    }

    [Fact]
    public async Task UpdateOutCompanyBulkAsync_ShouldRecalculate_AndFailWhenAnyLocked()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var empA = new Employee
        {
            Id = 80,
            CompanyId = 1,
            Code = "OUT030",
            Name = "Out Emp A",
            Type = EmployeeType.Daily,
            DailySalary = 200m,
            IsActive = true
        };
        empA.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Site 1");
        empA.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));

        var empB = new Employee
        {
            Id = 81,
            CompanyId = 1,
            Code = "OUT031",
            Name = "Out Emp B",
            Type = EmployeeType.Daily,
            DailySalary = 200m,
            IsActive = true
        };
        empB.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Site 2");
        empB.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 7, 31));

        database.Context.Employees.AddRange(empA, empB);
        await database.Context.SaveChangesAsync();

        var bulkAdd = await service.AddOutCompanyBulkAsync(new BulkOutCompanyPayrollEntryRequest(
            Entries:
            [
                new IndividualOutCompanyPayrollEntryRequest(EmployeeId: 80, PresentDays: 5, WorkedDaysByDayUnit: 5m),
                new IndividualOutCompanyPayrollEntryRequest(EmployeeId: 81, PresentDays: 5, WorkedDaysByDayUnit: 5m)
            ],
            DefaultStartDate: new DateOnly(2026, 8, 1),
            DefaultEndDate: new DateOnly(2026, 8, 10)));
        Assert.True(bulkAdd.IsSuccess);

        var idA = bulkAdd.Value[0].Id;
        var idB = bulkAdd.Value[1].Id;

        // Act - Bulk update both
        var bulkUpdate = await service.UpdateOutCompanyBulkAsync(new BulkOutCompanyPayrollEntryUpdateRequest(
            Entries:
            [
                new IndividualOutCompanyPayrollEntryUpdateRequest(Id: idA, PresentDays: 7, WorkedDaysByDayUnit: 7m),
                new IndividualOutCompanyPayrollEntryUpdateRequest(Id: idB, PresentDays: 8, WorkedDaysByDayUnit: 8m)
            ],
            DefaultStartDate: new DateOnly(2026, 8, 1),
            DefaultEndDate: new DateOnly(2026, 8, 10)));
        Assert.True(bulkUpdate.IsSuccess);
        Assert.Equal(1400m, bulkUpdate.Value[0].CalculatedSalary); // 7 * 200
        Assert.Equal(1600m, bulkUpdate.Value[1].CalculatedSalary); // 8 * 200

        // Move salary for A
        var moveResult = await service.MoveSalaryForEmployeeAccountAsync(idA, new PayrollEntrySalaryPaymentRequest(
            PostingDate: new DateOnly(2026, 8, 11)));
        Assert.True(moveResult.IsSuccess);

        // Act again - Should fail atomically because A is locked
        var failedUpdate = await service.UpdateOutCompanyBulkAsync(new BulkOutCompanyPayrollEntryUpdateRequest(
            Entries:
            [
                new IndividualOutCompanyPayrollEntryUpdateRequest(Id: idA, PresentDays: 7, WorkedDaysByDayUnit: 7m, Bonus: 50m),
                new IndividualOutCompanyPayrollEntryUpdateRequest(Id: idB, PresentDays: 8, WorkedDaysByDayUnit: 8m, Bonus: 50m)
            ],
            DefaultStartDate: new DateOnly(2026, 8, 1),
            DefaultEndDate: new DateOnly(2026, 8, 10)));
        Assert.True(failedUpdate.IsFailure);
        Assert.Equal("PayrollEntry.AlreadyPaid", failedUpdate.Error.Code);
    }

    [Fact]
    public async Task AddOutCompanyAsync_ShouldRespectUserSuppliedStartDate_AndNotOverrideWithLastDayOfReceivingSalary()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 90,
            CompanyId = 1,
            Code = "OUT090",
            Name = "Explicit Date Worker",
            Type = EmployeeType.Monthly,
            MonthlySalary = 6000m,
            RequiredWorkingDaysPerMonth = 30,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Remote Base");
        outEmp.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 8, 31)); // Last day is Aug 31
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        // User supplies custom StartDate: Sept 10 (NOT Sept 1)
        var userStartDate = new DateOnly(2026, 9, 10);
        var userEndDate = new DateOnly(2026, 9, 25);

        var request = new OutCompanyPayrollEntryRequest(
            EmployeeId: 90,
            StartDate: userStartDate,
            EndDate: userEndDate,
            WorkedDaysByDayUnit: 15m,
            Bonus: 500m,
            Deduction: 100m,
            PresentDays: 15);

        // Act
        var result = await service.AddOutCompanyAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        // Backend must NOT replace StartDate with LastDayOfReceivingSalary + 1 day (2026-09-01)
        Assert.Equal(userStartDate, result.Value.StartDate);
        Assert.Equal(userEndDate, result.Value.EndDate);
    }

    [Fact]
    public async Task UpdateOutCompanyAsync_ShouldRespectUserSuppliedDates_AndValidateDateRange()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 91,
            CompanyId = 1,
            Code = "OUT091",
            Name = "Update Date Worker",
            Type = EmployeeType.Daily,
            DailySalary = 250m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Field Alpha");
        outEmp.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 8, 31));
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        var createResult = await service.AddOutCompanyAsync(new OutCompanyPayrollEntryRequest(
            EmployeeId: 91,
            StartDate: new DateOnly(2026, 9, 1),
            EndDate: new DateOnly(2026, 9, 10),
            PresentDays: 10,
            WorkedDaysByDayUnit: 10m));
        Assert.True(createResult.IsSuccess);
        var entryId = createResult.Value.Id;

        // Act 1: Update with valid changed dates
        var updatedStartDate = new DateOnly(2026, 9, 5);
        var updatedEndDate = new DateOnly(2026, 9, 15);
        var updateResult = await service.UpdateOutCompanyAsync(entryId, new OutCompanyPayrollEntryUpdateRequest(
            StartDate: updatedStartDate,
            EndDate: updatedEndDate,
            PresentDays: 10,
            WorkedDaysByDayUnit: 10m));

        Assert.True(updateResult.IsSuccess);
        Assert.Equal(updatedStartDate, updateResult.Value.StartDate);
        Assert.Equal(updatedEndDate, updateResult.Value.EndDate);

        // Act 2: Attempt update with StartDate > EndDate
        var invalidUpdate = await service.UpdateOutCompanyAsync(entryId, new OutCompanyPayrollEntryUpdateRequest(
            StartDate: new DateOnly(2026, 9, 20),
            EndDate: new DateOnly(2026, 9, 10),
            PresentDays: 10,
            WorkedDaysByDayUnit: 10m));

        Assert.True(invalidUpdate.IsFailure);
        Assert.Equal("PayrollEntry.InvalidDateRange", invalidUpdate.Error.Code);
    }

    [Fact]
    public async Task AddOutCompanyAsync_ShouldFail_WhenStartDateGreaterThanEndDate()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 92,
            CompanyId = 1,
            Code = "OUT092",
            Name = "Invalid Range Worker",
            Type = EmployeeType.Daily,
            DailySalary = 200m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Site Invalid");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        var request = new OutCompanyPayrollEntryRequest(
            EmployeeId: 92,
            StartDate: new DateOnly(2026, 9, 20),
            EndDate: new DateOnly(2026, 9, 10),
            PresentDays: 5,
            WorkedDaysByDayUnit: 5m);

        // Act
        var result = await service.AddOutCompanyAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("PayrollEntry.InvalidDateRange", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_InCompany_ShouldEnforceStartDateFromLastDayPlusOne()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp = await database.Context.Employees.FindAsync(1);
        emp!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 8, 31));
        await database.Context.SaveChangesAsync();

        for (int day = 1; day <= 15; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 9, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var createResult = await service.AddAsync(new PayrollEntryCreateRequest(
            EmployeeId: 1,
            EndDate: new DateOnly(2026, 9, 10)));
        Assert.True(createResult.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 1), createResult.Value.StartDate);

        // Act - Update InCompany entry with new EndDate
        var updateResult = await service.UpdateAsync(createResult.Value.Id, new PayrollEntryUpdateRequest(
            EndDate: new DateOnly(2026, 9, 15)));

        // Assert - StartDate remains authoritatively LastDayOfReceivingSalary + 1 day
        Assert.True(updateResult.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 1), updateResult.Value.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 15), updateResult.Value.EndDate);
        Assert.Equal(15, updateResult.Value.AttendanceSummary.PresentDays);
    }

    [Fact]
    public async Task UpdateBulkAsync_InCompany_ShouldEnforceStartDateFromLastDayPlusOne()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var emp1 = await database.Context.Employees.FindAsync(1);
        emp1!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 8, 31));
        var emp2 = await database.Context.Employees.FindAsync(2);
        emp2!.UpdateLastDayOfReceivingSalary(new DateOnly(2026, 8, 31));
        await database.Context.SaveChangesAsync();

        for (int day = 1; day <= 10; day++)
        {
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 9, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
            database.Context.EmployeeAttendances.Add(new AttendanceEntity
            {
                CompanyId = 1,
                EmployeeId = 2,
                WorkDate = new DateOnly(2026, 9, day),
                Status = EmployeeAttendanceStatus.Present,
                WorkDayRatio = WorkDayRatio.FullDay
            });
        }
        await database.Context.SaveChangesAsync();

        var bulkAdd = await service.AddBulkAsync(new BulkPayrollEntryCreateRequest(
            Entries:
            [
                new IndividualPayrollEntryCreateRequest(EmployeeId: 1, EndDate: new DateOnly(2026, 9, 5)),
                new IndividualPayrollEntryCreateRequest(EmployeeId: 2, EndDate: new DateOnly(2026, 9, 5))
            ]));
        Assert.True(bulkAdd.IsSuccess);

        var id1 = bulkAdd.Value[0].Id;
        var id2 = bulkAdd.Value[1].Id;

        // Act - Bulk update InCompany entries
        var bulkUpdate = await service.UpdateBulkAsync(new BulkPayrollEntryUpdateRequest(
            Entries:
            [
                new IndividualPayrollEntryUpdateRequest(Id: id1, EndDate: new DateOnly(2026, 9, 10)),
                new IndividualPayrollEntryUpdateRequest(Id: id2, EndDate: new DateOnly(2026, 9, 10))
            ]));

        // Assert - Both entries derive StartDate = 2026-09-01
        Assert.True(bulkUpdate.IsSuccess);
        Assert.Equal(2, bulkUpdate.Value.Count);
        Assert.Equal(new DateOnly(2026, 9, 1), bulkUpdate.Value[0].StartDate);
        Assert.Equal(new DateOnly(2026, 9, 10), bulkUpdate.Value[0].EndDate);
        Assert.Equal(new DateOnly(2026, 9, 1), bulkUpdate.Value[1].StartDate);
        Assert.Equal(new DateOnly(2026, 9, 10), bulkUpdate.Value[1].EndDate);
    }

    // ─── OUT COMPANY PAYROLL DATE & ABSENT DAYS RULES TESTS ──────────────────

    [Fact]
    public async Task Case1_FirstPayrollBeforeEmployeeCreationDate_ShouldBeValid()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 201,
            CompanyId = 1,
            Code = "OUT201",
            Name = "Pre-Hire Worker",
            Type = EmployeeType.Monthly,
            MonthlySalary = 6000m,
            RequiredWorkingDaysPerMonth = 30,
            IsActive = true,
            CreatedOn = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc)
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Desert Site");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        var startDate = new DateOnly(2026, 9, 5);
        var endDate = new DateOnly(2026, 9, 15);

        var request = new OutCompanyPayrollEntryRequest(
            EmployeeId: 201,
            StartDate: startDate,
            EndDate: endDate,
            PresentDays: 10,
            WorkedDaysByDayUnit: 10m);

        // Act
        var result = await service.AddOutCompanyAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(startDate, result.Value.StartDate);
        Assert.Equal(endDate, result.Value.EndDate);
        Assert.Equal(10, result.Value.AttendanceSummary.PresentDays);
        // TotalDays = (15 - 5) + 1 = 11. AbsentDays = 11 - 10 = 1.
        Assert.Equal(1, result.Value.AttendanceSummary.AbsentDays);
    }

    [Fact]
    public async Task Case2_FirstPayrollAfterEmployeeCreationDate_ShouldBeValid()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 202,
            CompanyId = 1,
            Code = "OUT202",
            Name = "Post-Hire Worker",
            Type = EmployeeType.Monthly,
            MonthlySalary = 6000m,
            RequiredWorkingDaysPerMonth = 30,
            IsActive = true,
            CreatedOn = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc)
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Desert Site");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        var startDate = new DateOnly(2026, 9, 17);
        var endDate = new DateOnly(2026, 9, 30);

        var request = new OutCompanyPayrollEntryRequest(
            EmployeeId: 202,
            StartDate: startDate,
            EndDate: endDate,
            PresentDays: 12,
            WorkedDaysByDayUnit: 12m);

        // Act
        var result = await service.AddOutCompanyAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(startDate, result.Value.StartDate);
        Assert.Equal(endDate, result.Value.EndDate);
        Assert.Equal(12, result.Value.AttendanceSummary.PresentDays);
        // TotalDays = (30 - 17) + 1 = 14. AbsentDays = 14 - 12 = 2.
        Assert.Equal(2, result.Value.AttendanceSummary.AbsentDays);
    }

    [Fact]
    public async Task Case3_OverlappingPreviousPayroll_ShouldBeInvalid()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 203,
            CompanyId = 1,
            Code = "OUT203",
            Name = "Overlap Worker",
            Type = EmployeeType.Daily,
            DailySalary = 200m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "North Site");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        // Seed previous payroll: 2026-09-01 to 2026-09-30
        var firstResult = await service.AddOutCompanyAsync(new OutCompanyPayrollEntryRequest(
            EmployeeId: 203,
            StartDate: new DateOnly(2026, 9, 1),
            EndDate: new DateOnly(2026, 9, 30),
            PresentDays: 25,
            WorkedDaysByDayUnit: 25m));
        Assert.True(firstResult.IsSuccess);

        // Act: Attempt new payroll: 2026-09-20 to 2026-10-10 (overlaps with previous ending 2026-09-30)
        var overlapResult = await service.AddOutCompanyAsync(new OutCompanyPayrollEntryRequest(
            EmployeeId: 203,
            StartDate: new DateOnly(2026, 9, 20),
            EndDate: new DateOnly(2026, 10, 10),
            PresentDays: 15,
            WorkedDaysByDayUnit: 15m));

        // Assert
        Assert.True(overlapResult.IsFailure);
        Assert.Equal("PayrollEntry.PeriodOverlap", overlapResult.Error.Code);
    }

    [Fact]
    public async Task Case4_ContinuousPayroll_ShouldBeValid()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 204,
            CompanyId = 1,
            Code = "OUT204",
            Name = "Continuous Worker",
            Type = EmployeeType.Daily,
            DailySalary = 200m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "North Site");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        // Seed previous payroll: 2026-09-01 to 2026-09-30
        var firstResult = await service.AddOutCompanyAsync(new OutCompanyPayrollEntryRequest(
            EmployeeId: 204,
            StartDate: new DateOnly(2026, 9, 1),
            EndDate: new DateOnly(2026, 9, 30),
            PresentDays: 25,
            WorkedDaysByDayUnit: 25m));
        Assert.True(firstResult.IsSuccess);

        // Act: Continuous new payroll: 2026-10-01 to 2026-10-31
        var nextResult = await service.AddOutCompanyAsync(new OutCompanyPayrollEntryRequest(
            EmployeeId: 204,
            StartDate: new DateOnly(2026, 10, 1),
            EndDate: new DateOnly(2026, 10, 31),
            PresentDays: 28,
            WorkedDaysByDayUnit: 28m));

        // Assert
        Assert.True(nextResult.IsSuccess);
        Assert.Equal(new DateOnly(2026, 10, 1), nextResult.Value.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 31), nextResult.Value.EndDate);
        Assert.Equal(28, nextResult.Value.AttendanceSummary.PresentDays);
        // TotalDays = 31, AbsentDays = 31 - 28 = 3.
        Assert.Equal(3, nextResult.Value.AttendanceSummary.AbsentDays);
    }

    [Fact]
    public async Task Case5_AbsentDays_Calculation_ShouldBeAccurate()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 205,
            CompanyId = 1,
            Code = "OUT205",
            Name = "Absent Calculation Worker",
            Type = EmployeeType.Daily,
            DailySalary = 150m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "South Site");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        // Start = 2026-09-01, End = 2026-09-30, PresentDays = 25 -> TotalDays = 30, AbsentDays = 5
        var request = new OutCompanyPayrollEntryRequest(
            EmployeeId: 205,
            StartDate: new DateOnly(2026, 9, 1),
            EndDate: new DateOnly(2026, 9, 30),
            PresentDays: 25,
            WorkedDaysByDayUnit: 25m);

        // Act
        var result = await service.AddOutCompanyAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.Value.AttendanceSummary.PresentDays);
        Assert.Equal(5, result.Value.AttendanceSummary.AbsentDays);

        var dbEntry = await database.Context.PayrollEntries.FindAsync(result.Value.Id);
        Assert.NotNull(dbEntry);
        Assert.Equal(25, dbEntry.PresentDays);
        Assert.Equal(5, dbEntry.AbsentDays);
    }

    [Fact]
    public async Task Case6_InvalidPresentDays_GreaterThanTotalDays_ShouldBeInvalid()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 206,
            CompanyId = 1,
            Code = "OUT206",
            Name = "Excessive Present Worker",
            Type = EmployeeType.Daily,
            DailySalary = 150m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "South Site");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        // TotalDays = (30 - 1) + 1 = 30. PresentDays = 31 -> INVALID
        var request = new OutCompanyPayrollEntryRequest(
            EmployeeId: 206,
            StartDate: new DateOnly(2026, 9, 1),
            EndDate: new DateOnly(2026, 9, 30),
            PresentDays: 31,
            WorkedDaysByDayUnit: 30m);

        // Act
        var result = await service.AddOutCompanyAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("PayrollEntry.InvalidPresentDays", result.Error.Code);
    }

    [Fact]
    public async Task UpdateOutCompanyAsync_ShouldExcludeSelf_AndCalculateAbsentDays()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 207,
            CompanyId = 1,
            Code = "OUT207",
            Name = "Update Out Worker",
            Type = EmployeeType.Daily,
            DailySalary = 200m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Site 207");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        var create = await service.AddOutCompanyAsync(new OutCompanyPayrollEntryRequest(
            EmployeeId: 207,
            StartDate: new DateOnly(2026, 9, 1),
            EndDate: new DateOnly(2026, 9, 20),
            PresentDays: 18,
            WorkedDaysByDayUnit: 18m));
        Assert.True(create.IsSuccess);

        // Act: Update dates on same entry to 2026-09-01 to 2026-09-25 with PresentDays = 22
        var update = await service.UpdateOutCompanyAsync(create.Value.Id, new OutCompanyPayrollEntryUpdateRequest(
            StartDate: new DateOnly(2026, 9, 1),
            EndDate: new DateOnly(2026, 9, 25),
            PresentDays: 22,
            WorkedDaysByDayUnit: 22m));

        // Assert
        Assert.True(update.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 1), update.Value.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 25), update.Value.EndDate);
        Assert.Equal(22, update.Value.AttendanceSummary.PresentDays);
        // TotalDays = 25, AbsentDays = 25 - 22 = 3
        Assert.Equal(3, update.Value.AttendanceSummary.AbsentDays);
    }

    [Fact]
    public async Task UpdateOutCompanyAsync_ShouldFail_WhenFinanciallyLocked()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var outEmp = new Employee
        {
            Id = 208,
            CompanyId = 1,
            Code = "OUT208",
            Name = "Locked Out Worker",
            Type = EmployeeType.Daily,
            DailySalary = 200m,
            IsActive = true
        };
        outEmp.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Site 208");
        database.Context.Employees.Add(outEmp);
        await database.Context.SaveChangesAsync();

        var create = await service.AddOutCompanyAsync(new OutCompanyPayrollEntryRequest(
            EmployeeId: 208,
            StartDate: new DateOnly(2026, 9, 1),
            EndDate: new DateOnly(2026, 9, 15),
            PresentDays: 14,
            WorkedDaysByDayUnit: 14m));
        Assert.True(create.IsSuccess);

        // Manually lock the payroll entry
        var entry = await database.Context.PayrollEntries.FindAsync(create.Value.Id);
        entry!.IsSalaryMovedToEmployeeAccount = true;
        await database.Context.SaveChangesAsync();

        // Act
        var update = await service.UpdateOutCompanyAsync(create.Value.Id, new OutCompanyPayrollEntryUpdateRequest(
            StartDate: new DateOnly(2026, 9, 1),
            EndDate: new DateOnly(2026, 9, 16),
            PresentDays: 15,
            WorkedDaysByDayUnit: 15m));

        // Assert
        Assert.True(update.IsFailure);
        Assert.Equal("PayrollEntry.AlreadyPaid", update.Error.Code);
    }

    [Fact]
    public async Task BulkOutCompany_AddAndUpdate_ShouldRespectDatesAndCalculateAbsentDays()
    {
        // Arrange
        await using var database = await PayrollEntryTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreatePayrollService();

        var empA = new Employee
        {
            Id = 210,
            CompanyId = 1,
            Code = "OUT210",
            Name = "Bulk Worker A",
            Type = EmployeeType.Daily,
            DailySalary = 300m,
            IsActive = true
        };
        empA.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Field Alpha");

        var empB = new Employee
        {
            Id = 211,
            CompanyId = 1,
            Code = "OUT211",
            Name = "Bulk Worker B",
            Type = EmployeeType.Daily,
            DailySalary = 350m,
            IsActive = true
        };
        empB.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Field Beta");

        database.Context.Employees.AddRange(empA, empB);
        await database.Context.SaveChangesAsync();

        // Act 1: Bulk Create
        var bulkAddResult = await service.AddOutCompanyBulkAsync(new BulkOutCompanyPayrollEntryRequest(
            DefaultStartDate: new DateOnly(2026, 9, 1),
            DefaultEndDate: new DateOnly(2026, 9, 15),
            Entries:
            [
                new IndividualOutCompanyPayrollEntryRequest(
                    EmployeeId: 210,
                    PresentDays: 12,
                    WorkedDaysByDayUnit: 12m),
                new IndividualOutCompanyPayrollEntryRequest(
                    EmployeeId: 211,
                    PresentDays: 14,
                    WorkedDaysByDayUnit: 14m)
            ]));

        // Assert 1
        Assert.True(bulkAddResult.IsSuccess);
        Assert.Equal(2, bulkAddResult.Value.Count);
        // TotalDays = (15 - 1) + 1 = 15.
        // Worker A: AbsentDays = 15 - 12 = 3.
        // Worker B: AbsentDays = 15 - 14 = 1.
        Assert.Equal(3, bulkAddResult.Value[0].AttendanceSummary.AbsentDays);
        Assert.Equal(1, bulkAddResult.Value[1].AttendanceSummary.AbsentDays);

        var idA = bulkAddResult.Value[0].Id;
        var idB = bulkAddResult.Value[1].Id;

        // Act 2: Bulk Update
        var bulkUpdateResult = await service.UpdateOutCompanyBulkAsync(new BulkOutCompanyPayrollEntryUpdateRequest(
            DefaultStartDate: new DateOnly(2026, 9, 1),
            DefaultEndDate: new DateOnly(2026, 9, 20),
            Entries:
            [
                new IndividualOutCompanyPayrollEntryUpdateRequest(
                    Id: idA,
                    PresentDays: 18,
                    WorkedDaysByDayUnit: 18m),
                new IndividualOutCompanyPayrollEntryUpdateRequest(
                    Id: idB,
                    PresentDays: 19,
                    WorkedDaysByDayUnit: 19m)
            ]));

        // Assert 2
        Assert.True(bulkUpdateResult.IsSuccess);
        Assert.Equal(2, bulkUpdateResult.Value.Count);
        // TotalDays = (20 - 1) + 1 = 20.
        // Worker A: AbsentDays = 20 - 18 = 2.
        // Worker B: AbsentDays = 20 - 19 = 1.
        Assert.Equal(2, bulkUpdateResult.Value[0].AttendanceSummary.AbsentDays);
        Assert.Equal(1, bulkUpdateResult.Value[1].AttendanceSummary.AbsentDays);
    }

    [Fact]
    public void Deserialize_OutCompanyBulk_ExactPayload_ShouldSucceed()
    {
        var json = """
        {
          "entries": [
            {
              "employeeId": 1,
              "startDate": "2026-09-17",
              "endDate": "2026-09-30",
              "presentDays": 10,
              "workedDaysByDayUnit": 10,
              "overtimeByDayUnit": 0,
              "deductionByDayUnit": 0,
              "bonus": 0,
              "deduction": 0
            }
          ]
        }
        """;

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new MiniErp.Api.Serialization.FlexibleDateOnlyJsonConverter());
        options.Converters.Add(new MiniErp.Api.Serialization.FlexibleNullableDateOnlyJsonConverter());

        var result = System.Text.Json.JsonSerializer.Deserialize<BulkOutCompanyPayrollEntryRequest>(json, options);

        Assert.NotNull(result);
        Assert.Single(result.Entries);
        Assert.Equal(1, result.Entries[0].EmployeeId);
        Assert.Equal(new DateOnly(2026, 9, 17), result.Entries[0].StartDate);
        Assert.Equal(new DateOnly(2026, 9, 30), result.Entries[0].EndDate);

        // Test ISO datetime format e.g. from frontend / swagger
        var jsonWithIso = """
        {
          "entries": [
            {
              "employeeId": 1,
              "startDate": "2026-09-17T00:00:00Z",
              "endDate": "2026-09-30T00:00:00.000Z",
              "presentDays": 10,
              "workedDaysByDayUnit": 10
            }
          ]
        }
        """;
        var resultIso = System.Text.Json.JsonSerializer.Deserialize<BulkOutCompanyPayrollEntryRequest>(jsonWithIso, options);
        Assert.NotNull(resultIso);
        Assert.Equal(new DateOnly(2026, 9, 17), resultIso.Entries[0].StartDate);
    }
}
