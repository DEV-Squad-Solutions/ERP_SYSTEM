using Microsoft.EntityFrameworkCore;
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
                Status: AttendanceStatus.Present,
                WorkDate: workDate,
                CheckIn: new TimeOnly(9, 0),
                CheckOut: new TimeOnly(17, 0)
            ),
            new IndividualAttendanceRecordRequest(
                EmployeeId: 2,
                Status: AttendanceStatus.Absent,
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
        Assert.Equal(AttendanceStatus.Present, first.Status);
        Assert.Equal(new TimeOnly(8, 0), first.WorkHours); // 17:00 - 9:00 = 8 hours

        var second = result.Value.FirstOrDefault(x => x.EmployeeId == 2);
        Assert.NotNull(second);
        Assert.Equal("Employee Two", second.EmployeeName);
        Assert.Equal(AttendanceStatus.Absent, second.Status);
        Assert.Null(second.WorkHours);

        // Verify Database
        var dbRecords = await database.Context.EmployeeAttendances.ToListAsync();
        Assert.Equal(2, dbRecords.Count);
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
            Status = AttendanceStatus.Absent,
            WorkDate = workDate
        };
        database.Context.EmployeeAttendances.Add(existingRecord);
        await database.Context.SaveChangesAsync();

        var request = new BulkEmployeeAttendanceRequest(
        [
            new IndividualAttendanceRecordRequest(
                EmployeeId: 1,
                Status: AttendanceStatus.Present,
                WorkDate: workDate,
                CheckIn: new TimeOnly(9, 0),
                CheckOut: new TimeOnly(17, 0)
            ),
            new IndividualAttendanceRecordRequest(
                EmployeeId: 2,
                Status: AttendanceStatus.Present,
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
        
        Assert.Equal(AttendanceStatus.Present, dbRecords[0].Status);
        Assert.Equal(new TimeOnly(8, 0), dbRecords[0].WorkHours);

        Assert.Equal(AttendanceStatus.Present, dbRecords[1].Status);
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
                Status: AttendanceStatus.Present,
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
}
