using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.EmployeeAttendance;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MiniErp.Tests.EmployeeAttendance;

public sealed class EmployeeAttendanceServiceTests
{
    [Fact]
    public async Task Queries_DefaultToCurrentFiscalYearAndRejectCrossYearUpdate()
    {
        await using var database = await EmployeeAttendanceTestDatabase
            .CreateAsync(companyId: 1);
        await database.ConfigureSeparateFiscalYearsAsync();
        var service = database.CreateService();

        var currentCreated = await service.AddAsync(
            new EmployeeAttendanceRequest(
                EmployeeId: 1,
                Status: EmployeeAttendanceStatus.Present,
                WorkDate: new DateOnly(2025, 6, 1),
                CheckIn: new TimeOnly(9, 0),
                CheckOut: new TimeOnly(17, 0)));
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE FiscalYears
            SET IsCurrent = 2
            WHERE CompanyId = 1 AND IsCurrent = 1;
            UPDATE FiscalYears SET IsCurrent = 1 WHERE Id = 100;
            UPDATE FiscalYears SET IsCurrent = 0 WHERE IsCurrent = 2;
            """);
        var nextCreated = await service.AddAsync(
            new EmployeeAttendanceRequest(
                EmployeeId: 1,
                Status: EmployeeAttendanceStatus.Present,
                WorkDate: new DateOnly(2026, 6, 1),
                CheckIn: new TimeOnly(9, 0),
                CheckOut: new TimeOnly(17, 0)));
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE FiscalYears
            SET IsCurrent = 2
            WHERE CompanyId = 1 AND IsCurrent = 1;
            UPDATE FiscalYears SET IsCurrent = 1 WHERE Id = 1;
            UPDATE FiscalYears SET IsCurrent = 0 WHERE IsCurrent = 2;
            """);

        Assert.True(currentCreated.IsSuccess);
        Assert.Equal(1, currentCreated.Value.FiscalYearId);
        Assert.True(nextCreated.IsSuccess);
        Assert.Equal(100, nextCreated.Value.FiscalYearId);

        var currentList = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 });
        var nextList = await service.GetAllAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 20 },
            new EmployeeAttendanceFilterRequest(FiscalYearId: 100));
        var currentReport = await service.GetReportAsync(
            new EmployeeAttendanceReportRequest(
                StartDate: new DateOnly(2025, 1, 1),
                EndDate: new DateOnly(2025, 12, 31)));
        var nextReport = await service.GetReportAsync(
            new EmployeeAttendanceReportRequest(
                StartDate: new DateOnly(2026, 1, 1),
                EndDate: new DateOnly(2026, 12, 31),
                FiscalYearId: 100));
        var hiddenNextYear = await service.GetByIdAsync(nextCreated.Value.Id);
        var explicitNextYear = await service.GetByIdAsync(
            nextCreated.Value.Id,
            fiscalYearId: 100);
        var crossYearUpdate = await service.UpdateAsync(
            currentCreated.Value.Id,
            new EmployeeAttendanceUpdateRequest(
                EmployeeId: 1,
                Status: EmployeeAttendanceStatus.Present,
                WorkDate: new DateOnly(2026, 6, 2),
                CheckIn: new TimeOnly(9, 0),
                CheckOut: new TimeOnly(17, 0),
                WorkDayRatio: WorkDayRatio.FullDay));

        Assert.Equal(currentCreated.Value.Id, Assert.Single(currentList.Value.Items).Id);
        Assert.Equal(nextCreated.Value.Id, Assert.Single(nextList.Value.Items).Id);
        Assert.Equal(1, currentReport.Value.TotalPresentDays);
        Assert.Equal(1, nextReport.Value.TotalPresentDays);
        Assert.True(hiddenNextYear.IsFailure);
        Assert.Equal("EmployeeAttendance.NotFound", hiddenNextYear.Error.Code);
        Assert.True(explicitNextYear.IsSuccess);
        Assert.True(crossYearUpdate.IsFailure);
        Assert.Equal(
            "FiscalYears.QueryDateOutsideRange",
            crossYearUpdate.Error.Code);
    }

    [Fact]
    public async Task AddBulkAsync_ShouldCreateNewAttendances_WhenTheyDoNotExist()
    {
        // Arrange
        await using var database = await EmployeeAttendanceTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateService();

        var workDate = new DateOnly(2026, 8, 11);
        var request = new BulkEmployeeAttendanceRequest(
        [
            new IndividualAttendanceRecordRequest(
                EmployeeId: 1,
                Status: EmployeeAttendanceStatus.Present,
                WorkDate: workDate,
                CheckIn: new TimeOnly(9, 0),
                CheckOut: new TimeOnly(17, 0)
            ),
            new IndividualAttendanceRecordRequest(
                EmployeeId: 2,
                Status: EmployeeAttendanceStatus.Absent,
                WorkDate: workDate,
                CheckIn: null,
                CheckOut: null
            )
        ]);

        // Act
        var result = await service.AddBulkAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var first = result.Value.FirstOrDefault(x => x.EmployeeId == 1);
        Assert.NotNull(first);
        Assert.Equal("Employee One", first.EmployeeName);
        Assert.Equal(EmployeeAttendanceStatus.Present, first.Status);
        Assert.Equal(new TimeOnly(8, 0), first.WorkHours); // 17:00 - 9:00 = 8 hours

        var second = result.Value.FirstOrDefault(x => x.EmployeeId == 2);
        Assert.NotNull(second);
        Assert.Equal("Employee Two", second.EmployeeName);
        Assert.Equal(EmployeeAttendanceStatus.Absent, second.Status);
        Assert.Null(second.WorkHours);

        // Verify Database
        var dbRecords = await database.Context.EmployeeAttendances.ToListAsync();
        Assert.Equal(2, dbRecords.Count);
    }

    [Fact]
    public async Task AddBulkAsync_ShouldRejectDateOutsideFiscalYears()
    {
        await using var database = await EmployeeAttendanceTestDatabase
            .CreateAsync(companyId: 1);
        var service = database.CreateService();
        var request = new BulkEmployeeAttendanceRequest(
        [
            new IndividualAttendanceRecordRequest(
                EmployeeId: 1,
                Status: EmployeeAttendanceStatus.Present,
                WorkDate: new DateOnly(8, 9, 24),
                CheckIn: new TimeOnly(9, 0),
                CheckOut: new TimeOnly(17, 0))
        ]);

        var result = await service.AddBulkAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "FiscalYears.QueryDateOutsideRange",
            result.Error.Code);
        Assert.Empty(await database.Context.EmployeeAttendances.ToListAsync());
    }

    [Fact]
    public async Task AddBulkAsync_ShouldUpdateExistingAttendances_WhenTheyAlreadyExist()
    {
        // Arrange
        await using var database = await EmployeeAttendanceTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateService();
        var workDate = new DateOnly(2026, 8, 11);

        // First add one record directly
        var existingRecord = new Domain.Entities.Employees.EmployeeAttendance
        {
            CompanyId = 1,
            EmployeeId = 1,
            Status = EmployeeAttendanceStatus.Absent,
            WorkDate = workDate
        };
        database.Context.EmployeeAttendances.Add(existingRecord);
        await database.Context.SaveChangesAsync();

        var request = new BulkEmployeeAttendanceRequest(
        [
            new IndividualAttendanceRecordRequest(
                EmployeeId: 1,
                Status: EmployeeAttendanceStatus.Present,
                WorkDate: workDate,
                CheckIn: new TimeOnly(9, 0),
                CheckOut: new TimeOnly(17, 0)
            ),
            new IndividualAttendanceRecordRequest(
                EmployeeId: 2,
                Status: EmployeeAttendanceStatus.Present,
                WorkDate: workDate,
                CheckIn: new TimeOnly(10, 0),
                CheckOut: new TimeOnly(18, 0)
            )
        ]);

        // Act
        var result = await service.AddBulkAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        // Verify in DB that employee 1 is updated (from Absent to Present)
        var dbRecords = await database.Context.EmployeeAttendances.OrderBy(a => a.EmployeeId).ToListAsync();
        Assert.Equal(2, dbRecords.Count);
        
        Assert.Equal(EmployeeAttendanceStatus.Present, dbRecords[0].Status);
        Assert.Equal(new TimeOnly(8, 0), dbRecords[0].WorkHours);

        Assert.Equal(EmployeeAttendanceStatus.Present, dbRecords[1].Status);
        Assert.Equal(new TimeOnly(8, 0), dbRecords[1].WorkHours);
    }

    [Fact]
    public async Task AddBulkAsync_ShouldFail_WhenEmployeeDoesNotExistInCompany()
    {
        // Arrange
        await using var database = await EmployeeAttendanceTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateService();
        var workDate = new DateOnly(2026, 8, 11);

        var request = new BulkEmployeeAttendanceRequest(
        [
            new IndividualAttendanceRecordRequest(
                EmployeeId: 99, // Non-existent employee
                Status: EmployeeAttendanceStatus.Present,
                WorkDate: workDate,
                CheckIn: new TimeOnly(9, 0),
                CheckOut: new TimeOnly(17, 0)
            )
        ]);

        // Act
        var result = await service.AddBulkAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Employee.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_ShouldFail_WhenEmployeeIsOutCompany()
    {
        // Arrange
        await using var database = await EmployeeAttendanceTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateService();

        var outEmployee = new MiniErp.Domain.Entities.Employees.Employee
        {
            Id = 10,
            CompanyId = 1,
            Name = "OutCompany Guy",
            Type = EmployeeType.Monthly,
            MonthlySalary = 5000,
            IsActive = true
        };
        outEmployee.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Client Site");
        database.Context.Employees.Add(outEmployee);
        await database.Context.SaveChangesAsync();

        var request = new EmployeeAttendanceRequest(
            EmployeeId: 10,
            Status: EmployeeAttendanceStatus.Present,
            WorkDate: new DateOnly(2026, 8, 12),
            CheckIn: new TimeOnly(9, 0),
            CheckOut: new TimeOnly(17, 0));

        // Act
        var result = await service.AddAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("EmployeeAttendance.EmployeeNotEligible", result.Error.Code);
    }

    [Fact]
    public async Task GetEmployeeSelectAsync_ShouldOnlyReturnInCompanyActiveEmployees()
    {
        // Arrange
        await using var database = await EmployeeAttendanceTestDatabase.CreateAsync(companyId: 1);
        var service = database.CreateService();

        var outEmployee = new MiniErp.Domain.Entities.Employees.Employee
        {
            Id = 20,
            CompanyId = 1,
            Name = "OutCompany Staff",
            Type = EmployeeType.Monthly,
            MonthlySalary = 5000,
            IsActive = true
        };
        outEmployee.UpdateWorkPlace(WorkPlaceStatus.OutCompany, "Remote Site");

        var inactiveEmployee = new MiniErp.Domain.Entities.Employees.Employee
        {
            Id = 21,
            CompanyId = 1,
            Name = "Inactive Staff",
            Type = EmployeeType.Monthly,
            MonthlySalary = 5000,
            IsActive = false
        };
        inactiveEmployee.UpdateWorkPlace(WorkPlaceStatus.InCompany, null);

        database.Context.Employees.AddRange(outEmployee, inactiveEmployee);
        await database.Context.SaveChangesAsync();

        // Act
        var result = await service.GetEmployeeSelectAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value, e => e.Id == 20);
        Assert.DoesNotContain(result.Value, e => e.Id == 21);
        Assert.Contains(result.Value, e => e.Id == 1);
        Assert.Contains(result.Value, e => e.Id == 2);
    }
}
